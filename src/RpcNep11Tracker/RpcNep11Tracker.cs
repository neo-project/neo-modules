// Copyright (C) 2015-2021 The Neo Project.
//
//  The neo is free software distributed under the MIT software license, 
//  see the accompanying file LICENSE in the main directory of the
//  project or http://www.opensource.org/licenses/mit-license.php 
//  for more details. 
//  Redistribution and use in source and binary forms with or without
//  modifications are permitted.

using Neo;
using Neo.IO;
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
        private IStore _db;
        private ISnapshot _levelDbSnapshot;
        private NeoSystem neoSystem;
        private uint _currentHeight;
        private Block _currentBlock;
        private Dictionary<UInt160, ContractState> _assetCache = new Dictionary<UInt160, ContractState>();

        public override string Description => "Enquiries NEP-11 balances and transaction history of accounts through RPC";

        protected override void OnSystemLoaded(NeoSystem system)
        {
            if (system.Settings.Network != _network) return;
            neoSystem = system;
            string path = string.Format(_dbPath, neoSystem.Settings.Network.ToString("X8"));
            _db = neoSystem.LoadStore(GetFullPath(path));
            RpcServerPlugin.RegisterMethods(this, _network);
        }

        protected override void Configure()
        {
            _dbPath = GetConfiguration().GetSection("DBPath").Value ?? "Nep11BalanceData";
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

        private ContractState GetContract(DataCache snapshot, UInt160 asset)
        {
            if (!_assetCache.ContainsKey(asset))
            {
                _assetCache[asset] = NativeContract.ContractManagement.GetContract(snapshot, asset);
            }
            return _assetCache[asset];
        }

        void IPersistencePlugin.OnPersist(NeoSystem system, Block block, DataCache snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            if (system.Settings.Network != _network) return;
            // Start freshly with a new DBCache for each block.
            _currentHeight = block.Index;
            _currentBlock = block;

            ResetBatch();

            uint transferIndex = 0;
            var transfers = new List<TransferRecord>();
            foreach (Blockchain.ApplicationExecuted appExecuted in applicationExecutedList)
            {
                // Executions that fault won't modify storage, so we can skip them.
                if (appExecuted.VMState.HasFlag(VMState.FAULT)) continue;
                foreach (var notifyEventArgs in appExecuted.Notifications)
                {
                    if (!(notifyEventArgs?.State is VmArray stateItems) || stateItems.Count == 0)
                        continue;
                    var contract = GetContract(snapshot, notifyEventArgs.ScriptHash);
                    if (contract?.Manifest.SupportedStandards.Contains("NEP-11", StringComparer.OrdinalIgnoreCase) == false) continue;
                    HandleNotification(snapshot, notifyEventArgs.ScriptContainer, notifyEventArgs.ScriptHash, notifyEventArgs.EventName, stateItems, transfers, ref transferIndex);
                }
            }

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
                Console.WriteLine($"Fault:{record.from},{record.to},{record.tokenId.GetSpan().ToHexString()}");
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
            _levelDbSnapshot?.Commit();
        }

        bool IPersistencePlugin.ShouldThrowExceptionFromCommit(Exception ex)
        {
            return true;
        }

        private void HandleNotification(DataCache snapshot, IVerifiable scriptContainer, UInt160 scriptHash, string eventName, VmArray stateItems, List<TransferRecord> transfers, ref uint transferIndex)
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
            if (fromBytes == null && toBytes == null)
                return;

            var from = fromBytes == null ? UInt160.Zero : new UInt160(fromBytes);
            var to = toBytes == null ? UInt160.Zero : new UInt160(toBytes);
            var tokenId = tokenIdItem as ByteString;

            transfers.Add(new TransferRecord(scriptHash, from, to, tokenId, amountItem.GetInteger()));
            if (scriptContainer is Transaction transaction)
            {
                RecordTransferHistory(snapshot, scriptHash, from, to, tokenId, amountItem.GetInteger(), transaction.Hash, ref transferIndex);
            }
        }

        private void RecordTransferHistory(DataCache snapshot, UInt160 contractHash, UInt160 from, UInt160 to, ByteString tokenId, BigInteger amount, UInt256 txHash, ref uint transferIndex)
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
            _levelDbSnapshot.Put(Key(prefix, key), value.ToArray());
        }

        private void Delete(byte prefix, ISerializable key)
        {
            _levelDbSnapshot.Delete(Key(prefix, key));
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
            json["address"] = userScriptHash.ToAddress(neoSystem.Settings.AddressVersion);
            JArray transfersSent = new JArray();
            json["sent"] = transfersSent;
            JArray transfersReceived = new JArray();
            json["received"] = transfersReceived;
            AddTransfers(Nep11TransferSentPrefix, userScriptHash, startTime, endTime, transfersSent, true);
            AddTransfers(Nep11TransferReceivedPrefix, userScriptHash, startTime, endTime, transfersReceived, false);
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
            foreach (var (key, value) in _db.FindPrefix<Nep11BalanceKey, Nep11Balance>(prefix))
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
                balances.Add(new JObject()
                {
                    ["assethash"] = key.ToString(),
                    ["tokens"] = new JArray(map[key].Select(v => new JObject()
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

        private UInt160 GetScriptHashFromParam(string addressOrScriptHash)
        {
            return addressOrScriptHash.Length < 40 ?
                addressOrScriptHash.ToScriptHash(neoSystem.Settings.AddressVersion) : UInt160.Parse(addressOrScriptHash);
        }

        private void AddTransfers(byte dbPrefix, UInt160 userScriptHash, ulong startTime, ulong endTime,
         JArray parentJArray, bool isSent)
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
            var unsortList = new List<(Nep11TransferKey key, Nep11Transfer value)>();
            foreach (var (key, value) in transferPairs)
            {
                if (++resultCount > _maxResults) break;
                unsortList.Add((key, value));
            }

            foreach (var (key, value) in unsortList.OrderByDescending(l => l.key.TimestampMS))
            {
                JObject transfer = new JObject();
                transfer["timestamp"] = key.TimestampMS;
                transfer["assethash"] = key.AssetScriptHash.ToString();
                transfer["tokenid"] = key.Token.GetSpan().ToHexString();
                transfer[isSent ? "toaddress" : "fromaddress"] = value.UserScriptHash == UInt160.Zero ? null : value.UserScriptHash.ToAddress(neoSystem.Settings.AddressVersion);
                transfer["amount"] = value.Amount.ToString();
                transfer["blockindex"] = value.BlockIndex;
                transfer["transfernotifyindex"] = key.BlockXferNotificationIndex;
                transfer["txhash"] = value.TxHash.ToString();
                parentJArray.Add(transfer);
            }
        }

    }
}
