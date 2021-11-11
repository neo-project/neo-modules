using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins.Storage;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.VM.Types;
using Neo.Wallets;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using static System.IO.Path;
using VmArray = Neo.VM.Types.Array;

namespace Neo.Plugins
{
    record TransferRecord(UInt160 asset, UInt160 from, UInt160 to, ByteString tokenId, BigInteger amount);

    record BalanceChangeRecord(UInt160 user, UInt160 asset);

    public class TokensTracker : Plugin, IPersistencePlugin
    {
        private const byte Nep11BalancePrefix = 0xf8;
        private const byte Nep11TransferSentPrefix = 0xf9;
        private const byte Nep11TransferReceivedPrefix = 0xfa;
        private const byte Nep17BalancePrefix = 0xe8;
        private const byte Nep17TransferSentPrefix = 0xe9;
        private const byte Nep17TransferReceivedPrefix = 0xea;

        private readonly HashSet<string> _properties = new()
        {
            "name",
            "description",
            "image",
            "tokenURI"
        };
        private bool _shouldTrackHistory;
        private uint _maxResults;
        private uint _network;
        private string _dbPath;
        private IStore _db;
        private ISnapshot _levelDbSnapshot;
        private NeoSystem neoSystem;
        private uint _currentHeight;
        private Block _currentBlock;
        private Dictionary<UInt160, ContractState> _assetCache = new();
        private readonly List<TrackerBase> trackers = new();

        public override string Description => "Enquiries NEP-11 balances and transaction history of accounts through RPC";

        protected override void OnSystemLoaded(NeoSystem system)
        {
            if (system.Settings.Network != _network) return;
            neoSystem = system;
            string path = string.Format(_dbPath, neoSystem.Settings.Network.ToString("X8"));
            _db = neoSystem.LoadStore(GetFullPath(path));
            trackers.Add(new Nep11Tracker());
            trackers.Add(new Nep17Tracker());
            foreach (TrackerBase tracker in trackers)
                RpcServerPlugin.RegisterMethods(tracker, _network);
        }

        protected override void Configure()
        {
            _dbPath = GetConfiguration().GetSection("DBPath").Value ?? "TokensBalanceData";
            _shouldTrackHistory = (GetConfiguration().GetSection("TrackHistory").Value ?? true.ToString()) != false.ToString();
            _maxResults = uint.Parse(GetConfiguration().GetSection("MaxResults").Value ?? "1000");
            _network = uint.Parse(GetConfiguration().GetSection("Network").Value ?? "860833102");
        }

        private void ResetBatch()
        {
            _levelDbSnapshot?.Dispose();
            _levelDbSnapshot = _db.GetSnapshot();
            _assetCache.Clear();
        }

        void IPersistencePlugin.OnPersist(NeoSystem system, Block block, DataCache snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            if (system.Settings.Network != _network) return;
            // Start freshly with a new DBCache for each block.
            _currentHeight = block.Index;
            _currentBlock = block;

            ResetBatch();

            uint nep11TransferIndex = 0;
            uint nep17TransferIndex = 0;
            var transfers = new List<TransferRecord>();
            var balanceChangeRecords = new HashSet<BalanceChangeRecord>();

            foreach (Blockchain.ApplicationExecuted appExecuted in applicationExecutedList)
            {
                // Executions that fault won't modify storage, so we can skip them.
                if (appExecuted.VMState.HasFlag(VMState.FAULT)) continue;
                foreach (var notifyEventArgs in appExecuted.Notifications)
                {
                    if (notifyEventArgs.EventName != "Transfer" || !(notifyEventArgs?.State is VmArray stateItems) || stateItems.Count == 0)
                        continue;
                    var contract = GetContract(snapshot, notifyEventArgs.ScriptHash);
                    if (contract?.Manifest.SupportedStandards.Contains("NEP-11") == true)
                    {
                        HandleNotificationNep11(notifyEventArgs.ScriptContainer, notifyEventArgs.ScriptHash, stateItems, transfers, ref nep11TransferIndex);
                    }
                    if (contract?.Manifest.SupportedStandards.Contains("NEP-17") == true)
                    {
                        HandleNotificationNep17(notifyEventArgs.ScriptContainer, notifyEventArgs.ScriptHash, stateItems, balanceChangeRecords, ref nep17TransferIndex);
                    }
                }
            }

            // update nep11 balance
            var contracts = new Dictionary<UInt160, (bool isDivisible, ContractState state)>();
            foreach (var transferRecord in transfers)
            {
                if (!contracts.ContainsKey(transferRecord.asset))
                {
                    var state = GetContract(snapshot, transferRecord.asset);
                    var balanceMethod = state.Manifest.Abi.GetMethod("balanceOf", 1);
                    var balanceMethod2 = state.Manifest.Abi.GetMethod("balanceOf", 2);
                    if (balanceMethod == null && balanceMethod2 == null)
                    {
                        Log($"{state.Hash} is not nft!", LogLevel.Warning);
                        continue;
                    }
                    var isDivisible = balanceMethod2 != null;
                    contracts[transferRecord.asset] = (isDivisible, state);
                }
                var asset = contracts[transferRecord.asset];
                if (asset.isDivisible)
                {
                    SaveDivisibleNFTBalance(transferRecord, snapshot);
                }
                else
                {
                    SaveNFTBalance(transferRecord);
                }
            }

            //update nep17 balance 
            foreach (var balanceChangeRecord in balanceChangeRecords)
            {
                SaveNep17Balance(balanceChangeRecord, snapshot);
            }
        }

        #region SaveBalance

        private void SaveNep17Balance(BalanceChangeRecord balanceChanged, DataCache snapshot)
        {
            var key = new Nep17BalanceKey(balanceChanged.user, balanceChanged.asset);
            using ScriptBuilder sb = new ScriptBuilder();
            sb.EmitDynamicCall(balanceChanged.asset, "balanceOf", balanceChanged.user);
            using ApplicationEngine engine =
                ApplicationEngine.Run(sb.ToArray(), snapshot, settings: neoSystem.Settings);

            if (engine.State.HasFlag(VMState.FAULT) || engine.ResultStack.Count == 0)
            {
                Log($"Fault:{balanceChanged.user} get {balanceChanged.asset} balance fault", LogLevel.Warning);
                return;
            }

            var balanceItem = engine.ResultStack.Pop();
            if (balanceItem is not Integer)
            {
                Log($"Fault:{balanceChanged.user} get {balanceChanged.asset} balance not number", LogLevel.Warning);
                return;
            }

            var balance = balanceItem.GetInteger();

            if (balance.IsZero)
            {
                Delete(Nep17BalancePrefix, key);
                return;
            }

            Put(Nep17BalancePrefix, key, new TokenBalance { Balance = balance, LastUpdatedBlock = _currentHeight });
        }

        private void SaveDivisibleNFTBalance(TransferRecord record, DataCache snapshot)
        {
            using ScriptBuilder sb = new ScriptBuilder();
            sb.EmitDynamicCall(record.asset, "balanceOf", record.from, record.tokenId);
            sb.EmitDynamicCall(record.asset, "balanceOf", record.to, record.tokenId);
            using ApplicationEngine engine = ApplicationEngine.Run(sb.ToArray(), snapshot, settings: neoSystem.Settings);
            if (engine.State.HasFlag(VMState.FAULT) || engine.ResultStack.Count != 2)
            {
                Log($"Fault: from[{record.from}] to[{record.to}] get {record.asset} token [{record.tokenId.GetSpan().ToHexString()}] balance fault", LogLevel.Warning);
                return;
            }
            var toBalance = engine.ResultStack.Pop();
            var fromBalance = engine.ResultStack.Pop();
            if (toBalance is not Integer || fromBalance is not Integer)
            {
                Log($"Fault: from[{record.from}] to[{record.to}] get {record.asset} token [{record.tokenId.GetSpan().ToHexString()}] balance not number", LogLevel.Warning);
                return;
            }
            Put(Nep11BalancePrefix, new Nep11BalanceKey(record.to, record.asset, record.tokenId), new TokenBalance { Balance = toBalance.GetInteger(), LastUpdatedBlock = _currentHeight });
            Put(Nep11BalancePrefix, new Nep11BalanceKey(record.from, record.asset, record.tokenId), new TokenBalance { Balance = fromBalance.GetInteger(), LastUpdatedBlock = _currentHeight });
        }

        private void SaveNFTBalance(TransferRecord record)
        {
            if (record.from != UInt160.Zero)
            {
                Delete(Nep11BalancePrefix, new Nep11BalanceKey(record.from, record.asset, record.tokenId));
            }

            if (record.to != UInt160.Zero)
            {
                Put(Nep11BalancePrefix, new Nep11BalanceKey(record.to, record.asset, record.tokenId), new TokenBalance { Balance = 1, LastUpdatedBlock = _currentHeight });
            }
        }


        #endregion



        void IPersistencePlugin.OnCommit(NeoSystem system, Block block, DataCache snapshot)
        {
            if (system.Settings.Network != _network) return;
            _levelDbSnapshot?.Commit();
        }

        bool IPersistencePlugin.ShouldThrowExceptionFromCommit(Exception ex)
        {
            return true;
        }

        private TransferRecord GetTransferRecord(UInt160 asset, VmArray stateItems)
        {
            if (stateItems.Count < 3)
            {
                return null;
            }
            var fromItem = stateItems[0];
            var toItem = stateItems[1];
            var amountItem = stateItems[2];
            if (fromItem.NotNull() && fromItem is not ByteString)
                return null;
            if (toItem.NotNull() && toItem is not ByteString)
                return null;
            if (amountItem is not ByteString && amountItem is not Integer)
                return null;

            byte[] fromBytes = fromItem.IsNull ? null : fromItem.GetSpan().ToArray();
            if (fromBytes != null && fromBytes.Length != UInt160.Length)
                return null;
            byte[] toBytes = toItem.IsNull ? null : toItem.GetSpan().ToArray();
            if (toBytes != null && toBytes.Length != UInt160.Length)
                return null;
            if (fromBytes == null && toBytes == null)
                return null;

            var from = fromBytes == null ? UInt160.Zero : new UInt160(fromBytes);
            var to = toBytes == null ? UInt160.Zero : new UInt160(toBytes);
            if (stateItems.Count == 3)
            {
                return new TransferRecord(asset, from, to, null, amountItem.GetInteger());
            }
            if (stateItems.Count == 4 && (stateItems[3] is ByteString tokenId))
            {
                return new TransferRecord(asset, from, to, tokenId, amountItem.GetInteger());
            }
            return null;
        }

        private void HandleNotificationNep11(IVerifiable scriptContainer, UInt160 asset, VmArray stateItems, List<TransferRecord> transfers, ref uint transferIndex)
        {
            if (stateItems.Count != 4) return;
            var transferRecord = GetTransferRecord(asset, stateItems);
            if (transferRecord == null) return;

            transfers.Add(transferRecord);
            if (scriptContainer is Transaction transaction)
            {
                RecordTransferHistoryNep11(asset, transferRecord.from, transferRecord.to, transferRecord.tokenId, transferRecord.amount, transaction.Hash, ref transferIndex);
            }
        }

        private void HandleNotificationNep17(IVerifiable scriptContainer, UInt160 asset, VmArray stateItems, HashSet<BalanceChangeRecord> balanceChangeRecords, ref uint transferIndex)
        {
            if (stateItems.Count != 3) return;
            var transferRecord = GetTransferRecord(asset, stateItems);
            if (transferRecord == null) return;

            if (transferRecord.from != UInt160.Zero)
            {
                balanceChangeRecords.Add(new BalanceChangeRecord(transferRecord.from, asset));
            }
            if (transferRecord.to != UInt160.Zero)
            {
                balanceChangeRecords.Add(new BalanceChangeRecord(transferRecord.to, asset));
            }
            if (scriptContainer is Transaction transaction)
            {
                RecordTransferHistoryNep17(asset, transferRecord.from, transferRecord.to, transferRecord.amount, transaction.Hash, ref transferIndex);
            }
        }

        private void RecordTransferHistoryNep11(UInt160 contractHash, UInt160 from, UInt160 to, ByteString tokenId, BigInteger amount, UInt256 txHash, ref uint transferIndex)
        {
            if (!_shouldTrackHistory) return;
            if (from != UInt160.Zero)
            {
                Put(Nep11TransferSentPrefix,
                    new Nep11TransferKey(from, _currentBlock.Header.Timestamp, contractHash, tokenId, transferIndex),
                    new TokenTransfer
                    {
                        Amount = amount,
                        UserScriptHash = to,
                        BlockIndex = _currentHeight,
                        TxHash = txHash
                    });
            }

            if (to != UInt160.Zero)
            {
                Put(Nep11TransferReceivedPrefix,
                    new Nep11TransferKey(to, _currentBlock.Header.Timestamp, contractHash, tokenId, transferIndex),
                    new TokenTransfer
                    {
                        Amount = amount,
                        UserScriptHash = from,
                        BlockIndex = _currentHeight,
                        TxHash = txHash
                    });
            }
            transferIndex++;
        }

        private void RecordTransferHistoryNep17(UInt160 scriptHash, UInt160 from, UInt160 to, BigInteger amount, UInt256 txHash, ref uint transferIndex)
        {
            if (!_shouldTrackHistory) return;
            if (from != UInt160.Zero)
            {
                Put(Nep17TransferSentPrefix,
                    new Nep17TransferKey(from, _currentBlock.Header.Timestamp, scriptHash, transferIndex),
                    new TokenTransfer
                    {
                        Amount = amount,
                        UserScriptHash = to,
                        BlockIndex = _currentHeight,
                        TxHash = txHash
                    });
            }

            if (to != UInt160.Zero)
            {
                Put(Nep17TransferReceivedPrefix,
                    new Nep17TransferKey(to, _currentBlock.Header.Timestamp, scriptHash, transferIndex),
                    new TokenTransfer
                    {
                        Amount = amount,
                        UserScriptHash = from,
                        BlockIndex = _currentHeight,
                        TxHash = txHash
                    });
            }
            transferIndex++;
        }

        #region RpcMethod


        [RpcMethod]
        public JObject GetNep11Transfers(JArray _params)
        {
            if (!_shouldTrackHistory) throw new RpcException(-32601, "Method not found");
            UInt160 userScriptHash = GetScriptHashFromParam(_params[0].AsString());
            // If start time not present, default to 1 week of history.
            ulong startTime = _params.Count > 1 ? (ulong)_params[1].AsNumber() :
                (DateTime.UtcNow - TimeSpan.FromDays(7)).ToTimestampMS();
            ulong endTime = _params.Count > 2 ? (ulong)_params[2].AsNumber() : DateTime.UtcNow.ToTimestampMS();

            if (endTime < startTime) throw new RpcException(-32602, "Invalid params");

            JObject json = new JObject();
            json["address"] = userScriptHash.ToAddress(neoSystem.Settings.AddressVersion);
            JArray transfersSent = new JArray();
            json["sent"] = transfersSent;
            JArray transfersReceived = new JArray();
            json["received"] = transfersReceived;
            AddNep11Transfers(Nep11TransferSentPrefix, userScriptHash, startTime, endTime, transfersSent);
            AddNep11Transfers(Nep11TransferReceivedPrefix, userScriptHash, startTime, endTime, transfersReceived);
            return json;
        }

        [RpcMethod]
        public JObject GetNep11Balances(JArray _params)
        {
            UInt160 userScriptHash = GetScriptHashFromParam(_params[0].AsString());

            JObject json = new JObject();
            JArray balances = new JArray();
            json["address"] = userScriptHash.ToAddress(neoSystem.Settings.AddressVersion);
            json["balance"] = balances;

            var map = new Dictionary<UInt160, List<(string tokenid, string amount, uint height)>>();
            int count = 0;
            byte[] prefix = Key(Nep11BalancePrefix, userScriptHash);
            foreach (var (key, value) in _db.FindPrefix<Nep11BalanceKey, TokenBalance>(prefix))
            {
                if (NativeContract.ContractManagement.GetContract(neoSystem.StoreView, key.AssetScriptHash) is null)
                    continue;
                if (!map.ContainsKey(key.AssetScriptHash))
                {
                    map[key.AssetScriptHash] = new List<(string, string, uint)>();
                }
                map[key.AssetScriptHash].Add((key.Token.GetSpan().ToHexString(), value.Balance.ToString(), value.LastUpdatedBlock));
                count++;
                if (count >= _maxResults)
                {
                    break;
                }
            }
            foreach (var key in map.Keys)
            {
                balances.Add(new JObject
                {
                    ["assethash"] = key.ToString(),
                    ["tokens"] = new JArray(map[key].Select(v => new JObject
                    {
                        ["tokenid"] = v.tokenid,
                        ["amount"] = v.amount,
                        ["lastupdatedblock"] = v.height
                    })),
                });
            }
            return json;
        }

        [RpcMethod]
        public JObject GetNep11Properties(JArray _params)
        {
            UInt160 nep11Hash = GetScriptHashFromParam(_params[0].AsString());
            var tokenId = _params[1].AsString().HexToBytes();

            using var sb = new ScriptBuilder();
            sb.EmitDynamicCall(nep11Hash, "properties", CallFlags.ReadOnly, tokenId);
            using var snapshot = neoSystem.GetSnapshot();

            using var engine = ApplicationEngine.Run(sb.ToArray(), snapshot, settings: neoSystem.Settings);
            JObject json = new JObject();

            if (engine.State == VMState.HALT)
            {
                var map = engine.ResultStack.Pop<Map>();
                foreach (var keyValue in map)
                {
                    if (keyValue.Value is CompoundType) continue;
                    var key = keyValue.Key.GetString();
                    if (_properties.Contains(key))
                    {
                        json[key] = keyValue.Value.GetString();
                    }
                    else
                    {
                        json[key] = keyValue.Value.IsNull ? null : keyValue.Value.GetSpan().ToBase64();
                    }
                }
            }
            return json;
        }


        [RpcMethod]
        public JObject GetNep17Transfers(JArray _params)
        {
            if (!_shouldTrackHistory) throw new RpcException(-32601, "Method not found");
            UInt160 userScriptHash = GetScriptHashFromParam(_params[0].AsString());
            // If start time not present, default to 1 week of history.
            ulong startTime = _params.Count > 1 ? (ulong)_params[1].AsNumber() :
                (DateTime.UtcNow - TimeSpan.FromDays(7)).ToTimestampMS();
            ulong endTime = _params.Count > 2 ? (ulong)_params[2].AsNumber() : DateTime.UtcNow.ToTimestampMS();

            if (endTime < startTime) throw new RpcException(-32602, "Invalid params");

            JObject json = new JObject();
            json["address"] = userScriptHash.ToAddress(neoSystem.Settings.AddressVersion);
            JArray transfersSent = new JArray();
            json["sent"] = transfersSent;
            JArray transfersReceived = new JArray();
            json["received"] = transfersReceived;
            AddNep17Transfers(Nep17TransferSentPrefix, userScriptHash, startTime, endTime, transfersSent);
            AddNep17Transfers(Nep17TransferReceivedPrefix, userScriptHash, startTime, endTime, transfersReceived);
            return json;
        }

        [RpcMethod]
        public JObject GetNep17Balances(JArray _params)
        {
            UInt160 userScriptHash = GetScriptHashFromParam(_params[0].AsString());

            JObject json = new JObject();
            JArray balances = new JArray();
            json["address"] = userScriptHash.ToAddress(neoSystem.Settings.AddressVersion);
            json["balance"] = balances;

            int count = 0;
            byte[] prefix = Key(Nep17BalancePrefix, userScriptHash);
            foreach (var (key, value) in _db.FindPrefix<Nep17BalanceKey, TokenBalance>(prefix))
            {
                if (NativeContract.ContractManagement.GetContract(neoSystem.StoreView, key.AssetScriptHash) is null)
                    continue;

                balances.Add(new JObject
                {
                    ["assethash"] = key.AssetScriptHash.ToString(),
                    ["amount"] = value.Balance.ToString(),
                    ["lastupdatedblock"] = value.LastUpdatedBlock
                });
                count++;
                if (count >= _maxResults)
                {
                    break;
                }
            }
            return json;
        }


        #endregion


        #region Helper


        private ContractState GetContract(DataCache snapshot, UInt160 asset)
        {
            if (!_assetCache.ContainsKey(asset))
            {
                _assetCache[asset] = NativeContract.ContractManagement.GetContract(snapshot, asset);
            }
            return _assetCache[asset];
        }

        private UInt160 GetScriptHashFromParam(string addressOrScriptHash)
        {
            return addressOrScriptHash.Length < 40 ?
                addressOrScriptHash.ToScriptHash(neoSystem.Settings.AddressVersion) : UInt160.Parse(addressOrScriptHash);
        }

        private static byte[] Key(byte prefix, ISerializable key)
        {
            byte[] buffer = new byte[key.Size + 1];
            using (MemoryStream ms = new MemoryStream(buffer, true))
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                writer.Write(prefix);
                key.Serialize(writer);
            }
            return buffer;
        }

        private void Put(byte prefix, ISerializable key, ISerializable value)
        {
            _levelDbSnapshot.Put(Key(prefix, key), value.ToArray());
        }

        private void Delete(byte prefix, ISerializable key)
        {
            _levelDbSnapshot.Delete(Key(prefix, key));
        }

        private void AddNep11Transfers(byte dbPrefix, UInt160 userScriptHash, ulong startTime, ulong endTime, JArray parentJArray)
        {
            var transferPairs = QueryTransfers<Nep11TransferKey, TokenTransfer>(dbPrefix, userScriptHash, startTime, endTime).Take((int)_maxResults).ToList();
            foreach (var (key, value) in transferPairs.OrderByDescending(l => l.key.TimestampMS))
            {
                JObject transfer = ToJson(key, value, dbPrefix == Nep11TransferSentPrefix);
                transfer["tokenid"] = key.Token.GetSpan().ToHexString();
                parentJArray.Add(transfer);
            }
        }

        private void AddNep17Transfers(byte dbPrefix, UInt160 userScriptHash, ulong startTime, ulong endTime, JArray parentJArray)
        {
            var transferPairs = QueryTransfers<Nep17TransferKey, TokenTransfer>(dbPrefix, userScriptHash, startTime, endTime).Take((int)_maxResults).ToList();
            foreach (var (key, value) in transferPairs.OrderByDescending(l => l.key.TimestampMS))
            {
                parentJArray.Add(ToJson(key, value, dbPrefix == Nep17TransferSentPrefix));
            }
        }

        private JObject ToJson(TokenTransferKey key, TokenTransfer value, bool isSent)
        {
            JObject transfer = new JObject();
            transfer["timestamp"] = key.TimestampMS;
            transfer["assethash"] = key.AssetScriptHash.ToString();
            transfer[isSent ? "toaddress" : "fromaddress"] = value.UserScriptHash == UInt160.Zero ? null : value.UserScriptHash.ToAddress(neoSystem.Settings.AddressVersion);
            transfer["amount"] = value.Amount.ToString();
            transfer["blockindex"] = value.BlockIndex;
            transfer["transfernotifyindex"] = key.BlockXferNotificationIndex;
            transfer["txhash"] = value.TxHash.ToString();
            return transfer;
        }

        private IEnumerable<(TKey key, TValue val)> QueryTransfers<TKey, TValue>(byte dbPrefix, UInt160 userScriptHash, ulong startTime, ulong endTime)
            where TKey : ISerializable, new()
            where TValue : class, ISerializable, new()
        {
            var prefix = new[] { dbPrefix }.Concat(userScriptHash.ToArray()).ToArray();
            byte[] startTimeBytes, endTimeBytes;
            if (BitConverter.IsLittleEndian)
            {
                startTimeBytes = BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(startTime));
                endTimeBytes = BitConverter.GetBytes(BinaryPrimitives.ReverseEndianness(endTime));
            }
            else
            {
                startTimeBytes = BitConverter.GetBytes(startTime);
                endTimeBytes = BitConverter.GetBytes(endTime);
            }
            var transferPairs = _db.FindRange<TKey, TValue>(prefix.Concat(startTimeBytes).ToArray(), prefix.Concat(endTimeBytes).ToArray());
            return transferPairs;
        }
        #endregion
    }
}
