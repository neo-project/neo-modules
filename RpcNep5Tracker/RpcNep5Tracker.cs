using Microsoft.AspNetCore.Http;
using Neo.IO.Caching;
using Neo.IO.Data.LevelDB;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC;
using Neo.Persistence;
using Neo.Persistence.LevelDB;
using Neo.SmartContract;
using Neo.VM;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Snapshot = Neo.Persistence.Snapshot;

namespace Neo.Plugins
{
    public class RpcNep5Tracker : Plugin, IPersistencePlugin, IRpcPlugin
    {
        private const byte Nep5BalancePrefix = 0xf8;
        private const byte Nep5TransferSentPrefix = 0xf9;
        private const byte Nep5TransferReceivedPrefix = 0xfa;
        private DB _db;
        private DataCache<Nep5BalanceKey, Nep5Balance> _balances;
        private DataCache<Nep5TransferKey, Nep5Transfer> _transfersSent;
        private DataCache<Nep5TransferKey, Nep5Transfer> _transfersReceived;
        private WriteBatch _writeBatch;
        private bool _shouldTrackHistory;
        private bool _recordNullAddressHistory;
        private uint _maxResults;
        private bool _shouldTrackNonStandardMintTokensEvent;
        private Neo.IO.Data.LevelDB.Snapshot _levelDbSnapshot;

        public override void Configure()
        {
            if (_db == null)
            {
                var dbPath = GetConfiguration().GetSection("DBPath").Value ?? "Nep5BalanceData";
                _db = DB.Open(dbPath, new Options { CreateIfMissing = true });
            }
            _shouldTrackHistory = (GetConfiguration().GetSection("TrackHistory").Value ?? true.ToString()) != false.ToString();
            _recordNullAddressHistory = (GetConfiguration().GetSection("RecordNullAddressHistory").Value ?? false.ToString()) != false.ToString();
            _maxResults = uint.Parse(GetConfiguration().GetSection("MaxResults").Value ?? "1000");
            _shouldTrackNonStandardMintTokensEvent = (GetConfiguration().GetSection("TrackNonStandardMintTokens").Value ?? false.ToString()) != false.ToString();
        }

        private void ResetBatch()
        {
            _writeBatch = new WriteBatch();
            _levelDbSnapshot?.Dispose();
            _levelDbSnapshot = _db.GetSnapshot();
            ReadOptions dbOptions = new ReadOptions { FillCache = false, Snapshot = _levelDbSnapshot };
            _balances = new DbCache<Nep5BalanceKey, Nep5Balance>(_db, dbOptions, _writeBatch, Nep5BalancePrefix);
            if (_shouldTrackHistory)
            {
                _transfersSent =
                    new DbCache<Nep5TransferKey, Nep5Transfer>(_db, dbOptions, _writeBatch, Nep5TransferSentPrefix);
                _transfersReceived =
                    new DbCache<Nep5TransferKey, Nep5Transfer>(_db, dbOptions, _writeBatch, Nep5TransferReceivedPrefix);
            }
        }

        private void RecordTransferHistory(Snapshot snapshot, UInt160 scriptHash, UInt160 from, UInt160 to, BigInteger amount, UInt256 txHash, ref ushort transferIndex)
        {
            if (!_shouldTrackHistory) return;
            if (_recordNullAddressHistory || from != UInt160.Zero)
            {
                _transfersSent.Add(new Nep5TransferKey(from,
                        snapshot.GetHeader(snapshot.Height).Timestamp, scriptHash, transferIndex),
                    new Nep5Transfer
                    {
                        Amount = amount,
                        UserScriptHash = to,
                        BlockIndex = snapshot.Height,
                        TxHash = txHash
                    });
            }

            if (_recordNullAddressHistory || to != UInt160.Zero)
            {
                _transfersReceived.Add(new Nep5TransferKey(to,
                        snapshot.GetHeader(snapshot.Height).Timestamp, scriptHash, transferIndex),
                    new Nep5Transfer
                    {
                        Amount = amount,
                        UserScriptHash = from,
                        BlockIndex = snapshot.Height,
                        TxHash = txHash
                    });
            }
            transferIndex++;
        }

        private void HandleNotification(Snapshot snapshot, Transaction transaction, UInt160 scriptHash,
            VM.Types.Array stateItems,
            Dictionary<Nep5BalanceKey, Nep5Balance> nep5BalancesChanged, ref ushort transferIndex)
        {
            if (stateItems.Count == 0) return;
            // Event name should be encoded as a byte array.
            if (!(stateItems[0] is VM.Types.ByteArray)) return;
            var eventName = Encoding.UTF8.GetString(stateItems[0].GetByteArray());

            if (_shouldTrackNonStandardMintTokensEvent && eventName == "mintTokens")
            {
                if (stateItems.Count < 4) return;
                // This is not an official standard but at least one token uses it, and so it is needed for proper
                // balance tracking to support all tokens in use.
                if (!(stateItems[2] is VM.Types.ByteArray))
                    return;
                byte[] mintToBytes = stateItems[2].GetByteArray();
                if (mintToBytes.Length != 20) return;
                var mintTo = new UInt160(mintToBytes);

                var mintAmountItem = stateItems[3];
                if (!(mintAmountItem is VM.Types.ByteArray || mintAmountItem is VM.Types.Integer))
                    return;

                var toKey = new Nep5BalanceKey(mintTo, scriptHash);
                if (!nep5BalancesChanged.ContainsKey(toKey)) nep5BalancesChanged.Add(toKey, new Nep5Balance());
                RecordTransferHistory(snapshot, scriptHash, UInt160.Zero, mintTo, mintAmountItem.GetBigInteger(), transaction.Hash, ref transferIndex);
                return;
            }
            if (eventName != "transfer") return;
            if (stateItems.Count < 4) return;

            if (!(stateItems[1] is null) && !(stateItems[1] is VM.Types.ByteArray))
                return;
            if (!(stateItems[2] is null) && !(stateItems[2] is VM.Types.ByteArray))
                return;
            var amountItem = stateItems[3];
            if (!(amountItem is VM.Types.ByteArray || amountItem is VM.Types.Integer))
                return;
            byte[] fromBytes = stateItems[1]?.GetByteArray();
            if (fromBytes?.Length != 20) fromBytes = null;
            byte[] toBytes = stateItems[2]?.GetByteArray();
            if (toBytes?.Length != 20) toBytes = null;
            if (fromBytes == null && toBytes == null) return;
            var from = new UInt160(fromBytes);
            var to = new UInt160(toBytes);

            if (fromBytes != null)
            {
                var fromKey = new Nep5BalanceKey(from, scriptHash);
                if (!nep5BalancesChanged.ContainsKey(fromKey)) nep5BalancesChanged.Add(fromKey, new Nep5Balance());
            }

            if (toBytes != null)
            {
                var toKey = new Nep5BalanceKey(to, scriptHash);
                if (!nep5BalancesChanged.ContainsKey(toKey)) nep5BalancesChanged.Add(toKey, new Nep5Balance());
            }
            RecordTransferHistory(snapshot, scriptHash, from, to, amountItem.GetBigInteger(), transaction.Hash, ref transferIndex);
        }

        public void OnPersist(Snapshot snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            // Start freshly with a new DBCache for each block.
            ResetBatch();
            Dictionary<Nep5BalanceKey, Nep5Balance> nep5BalancesChanged = new Dictionary<Nep5BalanceKey, Nep5Balance>();

            ushort transferIndex = 0;
            foreach (Blockchain.ApplicationExecuted appExecuted in applicationExecutedList)
            {
                foreach (var executionResults in appExecuted.ExecutionResults)
                {
                    // Executions that fault won't modify storage, so we can skip them.
                    if (executionResults.VMState.HasFlag(VMState.FAULT)) continue;
                    foreach (var notifyEventArgs in executionResults.Notifications)
                    {
                        if (!(notifyEventArgs?.State is VM.Types.Array stateItems) || stateItems.Count == 0
                            || !(notifyEventArgs.ScriptContainer is Transaction transaction))
                            continue;
                        HandleNotification(snapshot, transaction, notifyEventArgs.ScriptHash, stateItems,
                            nep5BalancesChanged, ref transferIndex);
                    }
                }
            }

            foreach (var nep5BalancePair in nep5BalancesChanged)
            {
                // get guarantee accurate balances by calling balanceOf for keys that changed.
                byte[] script;
                using (ScriptBuilder sb = new ScriptBuilder())
                {
                    sb.EmitAppCall(nep5BalancePair.Key.AssetScriptHash, "balanceOf",
                        nep5BalancePair.Key.UserScriptHash.ToArray());
                    script = sb.ToArray();
                }

                ApplicationEngine engine = ApplicationEngine.Run(script, snapshot);
                if (engine.State.HasFlag(VMState.FAULT)) continue;
                if (engine.ResultStack.Count <= 0) continue;
                nep5BalancePair.Value.Balance = engine.ResultStack.Pop().GetBigInteger();
                nep5BalancePair.Value.LastUpdatedBlock = snapshot.Height;
                if (nep5BalancePair.Value.Balance == 0)
                {
                    _balances.Delete(nep5BalancePair.Key);
                    continue;
                }
                var itemToChange = _balances.GetAndChange(nep5BalancePair.Key, () => nep5BalancePair.Value);
                if (itemToChange != nep5BalancePair.Value)
                    itemToChange.FromReplica(nep5BalancePair.Value);
            }
        }

        public void OnCommit(Snapshot snapshot)
        {
            _balances.Commit();
            if (_shouldTrackHistory)
            {
                _transfersSent.Commit();
                _transfersReceived.Commit();
            }

            _db.Write(WriteOptions.Default, _writeBatch);
        }

        public bool ShouldThrowExceptionFromCommit(Exception ex)
        {
            return true;
        }

        private void AddTransfers(byte dbPrefix, UInt160 userScriptHash, uint startTime, uint endTime,
            JArray parentJArray)
        {
            var prefix = new[] { dbPrefix }.Concat(userScriptHash.ToArray()).ToArray();
            var startTimeBytes = BitConverter.GetBytes(startTime);
            var endTimeBytes = BitConverter.GetBytes(endTime);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(startTimeBytes);
                Array.Reverse(endTimeBytes);
            }

            var transferPairs = _db.FindRange<Nep5TransferKey, Nep5Transfer>(
                prefix.Concat(startTimeBytes).ToArray(),
                prefix.Concat(endTimeBytes).ToArray());

            int resultCount = 0;
            foreach (var transferPair in transferPairs)
            {
                if (++resultCount > _maxResults) break;
                JObject transfer = new JObject();
                transfer["timestamp"] = transferPair.Key.Timestamp;
                transfer["asset_hash"] = transferPair.Key.AssetScriptHash.ToArray().Reverse().ToHexString();
                transfer["transfer_address"] = transferPair.Value.UserScriptHash.ToAddress();
                transfer["amount"] = transferPair.Value.Amount.ToString();
                transfer["block_index"] = transferPair.Value.BlockIndex;
                transfer["transfer_notify_index"] = transferPair.Key.BlockXferNotificationIndex;
                transfer["tx_hash"] = transferPair.Value.TxHash.ToArray().Reverse().ToHexString();
                parentJArray.Add(transfer);
            }
        }

        private UInt160 GetScriptHashFromParam(string addressOrScriptHash)
        {
            return addressOrScriptHash.Length < 40 ?
                addressOrScriptHash.ToScriptHash() : UInt160.Parse(addressOrScriptHash);
        }
        private JObject GetNep5Transfers(JArray _params)
        {
            UInt160 userScriptHash = GetScriptHashFromParam(_params[0].AsString());
            // If start time not present, default to 1 week of history.
            uint startTime = _params.Count > 1 ? (uint)_params[1].AsNumber() :
                (DateTime.UtcNow - TimeSpan.FromDays(7)).ToTimestamp();
            uint endTime = _params.Count > 2 ? (uint)_params[2].AsNumber() : DateTime.UtcNow.ToTimestamp();

            if (endTime < startTime) throw new RpcException(-32602, "Invalid params");

            JObject json = new JObject();
            JArray transfersSent = new JArray();
            json["sent"] = transfersSent;
            JArray transfersReceived = new JArray();
            json["received"] = transfersReceived;
            json["address"] = userScriptHash.ToAddress();
            AddTransfers(Nep5TransferSentPrefix, userScriptHash, startTime, endTime, transfersSent);
            AddTransfers(Nep5TransferReceivedPrefix, userScriptHash, startTime, endTime, transfersReceived);
            return json;
        }

        private JObject GetNep5Balances(JArray _params)
        {
            UInt160 userScriptHash = GetScriptHashFromParam(_params[0].AsString());

            JObject json = new JObject();
            JArray balances = new JArray();
            json["balance"] = balances;
            json["address"] = userScriptHash.ToAddress();
            var dbCache = new DbCache<Nep5BalanceKey, Nep5Balance>(_db, null, null, Nep5BalancePrefix);
            byte[] prefix = userScriptHash.ToArray();
            foreach (var storageKeyValuePair in dbCache.Find(prefix))
            {
                JObject balance = new JObject();
                balance["asset_hash"] = storageKeyValuePair.Key.AssetScriptHash.ToArray().Reverse().ToHexString();
                balance["amount"] = storageKeyValuePair.Value.Balance.ToString();
                balance["last_updated_block"] = storageKeyValuePair.Value.LastUpdatedBlock;
                balances.Add(balance);
            }
            return json;
        }

        public void PreProcess(HttpContext context, string method, JArray _params)
        {
        }

        public JObject OnProcess(HttpContext context, string method, JArray _params)
        {
            if (_shouldTrackHistory && method == "getnep5transfers") return GetNep5Transfers(_params);
            return method == "getnep5balances" ? GetNep5Balances(_params) : null;
        }

        public void PostProcess(HttpContext context, string method, JArray _params, JObject result)
        {
        }
    }
}
