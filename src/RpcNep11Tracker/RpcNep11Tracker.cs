using Neo;
using Neo.IO;
using Neo.IO.Data.LevelDB;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Neo.Plugins.Storage;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.VM.Types;
using System;
using System.Linq;
using System.Collections.Generic;
using static System.IO.Path;
using VmArray = Neo.VM.Types.Array;
using System.Numerics;
using System.IO;
using Neo.IO.Json;
using Neo.Wallets;
using System.Buffers.Binary;
using SQLitePCL;

namespace RpcNep11Tracker
{
    record TransferRecord(UInt160 asset, UInt160 from, UInt160 to, ByteString tokenId, BigInteger amount);

    public partial class RpcNep11Tracker : Plugin, IPersistencePlugin
    {
        private const byte Nep11BalancePrefix = 0xf8;
        private const byte Nep11TransferSentPrefix = 0xf9;
        private const byte Nep11TransferReceivedPrefix = 0xfa;
        private readonly HashSet<string> _properties = new HashSet<string>
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
        private DB _db;
        private WriteBatch _writeBatch;
        private Snapshot _levelDbSnapshot;
        private NeoSystem neoSystem;

        private uint _currentHeight;
        private Block _currentBlock;

        public override string Description => "Enquiries NEP-11 balances and transaction history of accounts through RPC";

        protected override void OnSystemLoaded(NeoSystem system)
        {
            if (system.Settings.Network != _network) return;
            neoSystem = system;
            string path = string.Format(_dbPath, system.Settings.Network.ToString("X8"));
            _db = DB.Open(GetFullPath(path), new Options { CreateIfMissing = true });
            RpcServerPlugin.RegisterMethods(this, _network);
        }



        protected override void Configure()
        {
            _dbPath = GetConfiguration().GetSection("DBPath").Value ?? "Nep11BalanceData";
            _shouldTrackHistory = (GetConfiguration().GetSection("TrackHistory").Value ?? true.ToString()) != false.ToString();
            _maxResults = uint.Parse(GetConfiguration().GetSection("MaxResults").Value ?? "1000");
            _network = uint.Parse(GetConfiguration().GetSection("Network").Value ?? "5195086");
        }

        private void ResetBatch()
        {
            _writeBatch = new WriteBatch();
            _levelDbSnapshot?.Dispose();
            _levelDbSnapshot = _db.GetSnapshot();
        }


        void IPersistencePlugin.OnPersist(NeoSystem system, Block block, DataCache snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            if (system.Settings.Network != _network) return;
            // Start freshly with a new DBCache for each block.
            _currentHeight = block.Index;
            _currentBlock = block;

            ResetBatch();

            ushort transferIndex = 0;
            var transfers = new List<TransferRecord>();
            foreach (Blockchain.ApplicationExecuted appExecuted in applicationExecutedList)
            {
                // Executions that fault won't modify storage, so we can skip them.
                if (appExecuted.VMState.HasFlag(VMState.FAULT)) continue;
                foreach (var notifyEventArgs in appExecuted.Notifications)
                {
                    if (!(notifyEventArgs?.State is VmArray stateItems) || stateItems.Count == 0)
                        continue;
                    HandleNotification(snapshot, notifyEventArgs.ScriptContainer, notifyEventArgs.ScriptHash, notifyEventArgs.EventName, stateItems, transfers, ref transferIndex);
                }
            }

            var contracts = new Dictionary<UInt160, (bool isDivisible, ContractState state)>();

            foreach (var transferRecord in transfers)
            {
                if (!contracts.ContainsKey(transferRecord.asset))
                {
                    var state = NativeContract.ContractManagement.GetContract(snapshot, transferRecord.asset);
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
                    SaveDivisibleNFT(transferRecord, snapshot);
                }
                else
                {
                    SaveNFT(transferRecord);
                }
            }
        }

        private void SaveDivisibleNFT(TransferRecord record, DataCache snapshot)
        {
            using ScriptBuilder sb = new ScriptBuilder();
            sb.EmitDynamicCall(record.asset, "balanceOf", record.from, record.tokenId);
            sb.EmitDynamicCall(record.asset, "balanceOf", record.to, record.tokenId);
            using ApplicationEngine engine = ApplicationEngine.Run(sb.ToArray(), snapshot, settings: neoSystem.Settings);
            if (engine.State.HasFlag(VMState.FAULT) || engine.ResultStack.Count != 2)
            {
                return;
            }
            Put(Nep11BalancePrefix, new Nep11BalanceKey(record.to, record.asset, record.tokenId), new Nep11Balance() { Balance = engine.ResultStack.Pop().GetInteger(), LastUpdatedBlock = _currentHeight });
            Put(Nep11BalancePrefix, new Nep11BalanceKey(record.from, record.asset, record.tokenId), new Nep11Balance() { Balance = engine.ResultStack.Pop().GetInteger(), LastUpdatedBlock = _currentHeight });
        }

        private void SaveNFT(TransferRecord record)
        {
            if (record.from != UInt160.Zero)
            {
                Delete(Nep11BalancePrefix, new Nep11BalanceKey(record.from, record.asset, record.tokenId));
            }

            if (record.to != UInt160.Zero)
            {
                Put(Nep11BalancePrefix, new Nep11BalanceKey(record.to, record.asset, record.tokenId), new Nep11Balance() { Balance = 1, LastUpdatedBlock = _currentHeight });
            }
        }


        void IPersistencePlugin.OnCommit(NeoSystem system, Block block, DataCache snapshot)
        {
            if (system.Settings.Network != _network) return;
            _db.Write(WriteOptions.Default, _writeBatch);
        }

        bool IPersistencePlugin.ShouldThrowExceptionFromCommit(Exception ex)
        {
            return true;
        }


        private void HandleNotification(DataCache snapshot, IVerifiable scriptContainer, UInt160 scriptHash, string eventName, VmArray stateItems, List<TransferRecord> transfers, ref ushort transferIndex)
        {
            if (eventName != "Transfer") return;
            if (stateItems.Count != 4) return;

            var fromItem = stateItems[0];
            var toItem = stateItems[1];
            var amountItem = stateItems[2];
            var tokenIdItem = stateItems[3];
            if (fromItem.NotNull() && fromItem is not ByteString)
                return;
            if (toItem.NotNull() && toItem is not ByteString)
                return;
            if (amountItem is not ByteString && amountItem is not Integer)
                return;
            if (tokenIdItem.IsNull || tokenIdItem is not ByteString)
                return;


            byte[] fromBytes = fromItem.IsNull ? null : fromItem.GetSpan().ToArray();
            if (fromBytes != null && fromBytes.Length != UInt160.Length)
                return;
            byte[] toBytes = toItem.IsNull ? null : toItem.GetSpan().ToArray();
            if (toBytes != null && toBytes.Length != UInt160.Length)
                return;
            if (fromBytes == null && toBytes == null) return;

            var from = fromBytes == null ? UInt160.Zero : new UInt160(fromBytes);
            var to = toBytes == null ? UInt160.Zero : new UInt160(toBytes);
            var tokenId = tokenIdItem as ByteString;

            transfers.Add(new TransferRecord(scriptHash, from, to, tokenId, amountItem.GetInteger()));
            if (scriptContainer is Transaction transaction)
            {
                RecordTransferHistory(snapshot, scriptHash, from, to, tokenId, amountItem.GetInteger(), transaction.Hash, ref transferIndex);
            }
        }



        private void RecordTransferHistory(DataCache snapshot, UInt160 contractHash, UInt160 from, UInt160 to, ByteString tokenId, BigInteger amount, UInt256 txHash, ref ushort transferIndex)
        {
            if (!_shouldTrackHistory) return;
            if (from != UInt160.Zero)
            {
                Put(Nep11TransferSentPrefix,
                    new Nep11TransferKey(from, _currentBlock.Header.Timestamp, contractHash, tokenId, transferIndex),
                    new Nep11Transfer
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
                    new Nep11Transfer
                    {
                        Amount = amount,
                        UserScriptHash = from,
                        BlockIndex = _currentHeight,
                        TxHash = txHash
                    });
            }
            transferIndex++;
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
            _writeBatch.Put(Key(prefix, key), value.ToArray());
        }

        private void Delete(byte prefix, ISerializable key)
        {
            _writeBatch.Delete(Key(prefix, key));
        }

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
            JArray transfersSent = new JArray();
            json["sent"] = transfersSent;
            JArray transfersReceived = new JArray();
            json["received"] = transfersReceived;
            json["address"] = userScriptHash.ToAddress(neoSystem.Settings.AddressVersion);
            AddTransfers(Nep11TransferSentPrefix, userScriptHash, startTime, endTime, transfersSent);
            AddTransfers(Nep11TransferReceivedPrefix, userScriptHash, startTime, endTime, transfersReceived);
            return json;
        }


        [RpcMethod]
        public JObject GetNep11Balances(JArray _params)
        {
            UInt160 userScriptHash = GetScriptHashFromParam(_params[0].AsString());

            JObject json = new JObject();
            JArray balances = new JArray();
            json["balance"] = balances;
            json["address"] = userScriptHash.ToAddress(neoSystem.Settings.AddressVersion);

            using Iterator it = _db.NewIterator(ReadOptions.Default);
            byte[] prefix = Key(Nep11BalancePrefix, userScriptHash);
            for (it.Seek(prefix); it.Valid(); it.Next())
            {
                ReadOnlySpan<byte> key_bytes = it.Key();
                if (!key_bytes.StartsWith(prefix)) break;
                var key = key_bytes[1..].AsSerializable<Nep11BalanceKey>();
                if (NativeContract.ContractManagement.GetContract(neoSystem.StoreView, key.AssetScriptHash) is null)
                    continue;
                var value = it.Value().AsSerializable<Nep11Balance>();
                balances.Add(new JObject
                {
                    ["assethash"] = key.AssetScriptHash.ToString(),
                    ["tokenId"] = key.Token.GetSpan().ToHexString(),
                    ["amount"] = value.Balance.ToString(),
                    ["lastupdatedblock"] = value.LastUpdatedBlock
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
        private UInt160 GetScriptHashFromParam(string addressOrScriptHash)
        {
            return addressOrScriptHash.Length < 40 ?
                addressOrScriptHash.ToScriptHash(neoSystem.Settings.AddressVersion) : UInt160.Parse(addressOrScriptHash);
        }

        private void AddTransfers(byte dbPrefix, UInt160 userScriptHash, ulong startTime, ulong endTime,
         JArray parentJArray)
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

            var transferPairs = _db.FindRange<Nep11TransferKey, Nep11Transfer>(
                prefix.Concat(startTimeBytes).ToArray(),
                prefix.Concat(endTimeBytes).ToArray());

            int resultCount = 0;
            foreach (var (key, value) in transferPairs)
            {
                if (++resultCount > _maxResults) break;
                JObject transfer = new JObject();
                transfer["timestamp"] = key.TimestampMS;
                transfer["assethash"] = key.AssetScriptHash.ToString();
                transfer["tokenId"] = key.Token.GetSpan().ToHexString();
                transfer["transferaddress"] = value.UserScriptHash == UInt160.Zero ? null : value.UserScriptHash.ToAddress(neoSystem.Settings.AddressVersion);
                transfer["amount"] = value.Amount.ToString();
                transfer["blockindex"] = value.BlockIndex;
                transfer["transfernotifyindex"] = key.BlockXferNotificationIndex;
                transfer["txhash"] = value.TxHash.ToString();
                parentJArray.Add(transfer);
            }
        }

    }
}
