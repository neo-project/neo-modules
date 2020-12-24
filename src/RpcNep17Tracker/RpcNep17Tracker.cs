using Neo.IO;
using Neo.IO.Caching;
using Neo.IO.Data.LevelDB;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static System.IO.Path;

namespace Neo.Plugins
{
    public class RpcNep17Tracker : Plugin, IPersistencePlugin
    {
        private const byte Nep17BalancePrefix = 0xf8;
        private const byte Nep17TransferSentPrefix = 0xf9;
        private const byte Nep17TransferReceivedPrefix = 0xfa;
        private DB _db;
        private DataCache<Nep17BalanceKey, Nep17Balance> _balances;
        private DataCache<Nep17TransferKey, Nep17Transfer> _transfersSent;
        private DataCache<Nep17TransferKey, Nep17Transfer> _transfersReceived;
        private WriteBatch _writeBatch;
        private bool _shouldTrackHistory;
        private bool _recordNullAddressHistory;
        private uint _maxResults;
        private Snapshot _levelDbSnapshot;

        public override string Description => "Enquiries NEP-17 balances and transaction history of accounts through RPC";

        public RpcNep17Tracker()
        {
            RpcServerPlugin.RegisterMethods(this);
        }

        protected override void Configure()
        {
            if (_db == null)
            {
                var dbPath = GetConfiguration().GetSection("DBPath").Value ?? "Nep17BalanceData";
                _db = DB.Open(GetFullPath(dbPath), new Options { CreateIfMissing = true });
            }
            _shouldTrackHistory = (GetConfiguration().GetSection("TrackHistory").Value ?? true.ToString()) != false.ToString();
            _recordNullAddressHistory = (GetConfiguration().GetSection("RecordNullAddressHistory").Value ?? false.ToString()) != false.ToString();
            _maxResults = uint.Parse(GetConfiguration().GetSection("MaxResults").Value ?? "1000");
        }

        private void ResetBatch()
        {
            _writeBatch = new WriteBatch();
            _levelDbSnapshot?.Dispose();
            _levelDbSnapshot = _db.GetSnapshot();
            ReadOptions dbOptions = new ReadOptions { FillCache = false, Snapshot = _levelDbSnapshot };
            _balances = new DbCache<Nep17BalanceKey, Nep17Balance>(_db, dbOptions, _writeBatch, Nep17BalancePrefix);
            if (_shouldTrackHistory)
            {
                _transfersSent =
                    new DbCache<Nep17TransferKey, Nep17Transfer>(_db, dbOptions, _writeBatch, Nep17TransferSentPrefix);
                _transfersReceived =
                    new DbCache<Nep17TransferKey, Nep17Transfer>(_db, dbOptions, _writeBatch, Nep17TransferReceivedPrefix);
            }
        }

        private void RecordTransferHistory(StoreView snapshot, UInt160 scriptHash, UInt160 from, UInt160 to, BigInteger amount, UInt256 txHash, ref ushort transferIndex)
        {
            if (!_shouldTrackHistory) return;

            Header header = snapshot.GetHeader(snapshot.CurrentBlockHash);

            if (_recordNullAddressHistory || from != UInt160.Zero)
            {
                _transfersSent.Add(new Nep17TransferKey(from, header.Timestamp, scriptHash, transferIndex),
                    new Nep17Transfer
                    {
                        Amount = amount,
                        UserScriptHash = to,
                        BlockIndex = snapshot.Height,
                        TxHash = txHash
                    });
            }

            if (_recordNullAddressHistory || to != UInt160.Zero)
            {
                _transfersReceived.Add(new Nep17TransferKey(to, header.Timestamp, scriptHash, transferIndex),
                    new Nep17Transfer
                    {
                        Amount = amount,
                        UserScriptHash = from,
                        BlockIndex = snapshot.Height,
                        TxHash = txHash
                    });
            }
            transferIndex++;
        }

        private void HandleNotification(StoreView snapshot, IVerifiable scriptContainer, UInt160 scriptHash, string eventName,
            VM.Types.Array stateItems,
            Dictionary<Nep17BalanceKey, Nep17Balance> nep17BalancesChanged, ref ushort transferIndex)
        {
            if (stateItems.Count == 0) return;
            if (eventName != "Transfer") return;
            if (stateItems.Count < 3) return;

            if (!(stateItems[0].IsNull) && !(stateItems[0] is VM.Types.ByteString))
                return;
            if (!(stateItems[1].IsNull) && !(stateItems[1] is VM.Types.ByteString))
                return;
            var amountItem = stateItems[2];
            if (!(amountItem is VM.Types.ByteString || amountItem is VM.Types.Integer))
                return;
            byte[] fromBytes = stateItems[0].IsNull ? null : stateItems[0].GetSpan().ToArray();
            if (fromBytes != null && fromBytes.Length != UInt160.Length)
                return;
            byte[] toBytes = stateItems[1].IsNull ? null : stateItems[1].GetSpan().ToArray();
            if (toBytes != null && toBytes.Length != UInt160.Length)
                return;
            if (fromBytes == null && toBytes == null) return;

            var from = UInt160.Zero;
            var to = UInt160.Zero;

            if (fromBytes != null)
            {
                from = new UInt160(fromBytes);
                var fromKey = new Nep17BalanceKey(from, scriptHash);
                if (!nep17BalancesChanged.ContainsKey(fromKey)) nep17BalancesChanged.Add(fromKey, new Nep17Balance());
            }

            if (toBytes != null)
            {
                to = new UInt160(toBytes);
                var toKey = new Nep17BalanceKey(to, scriptHash);
                if (!nep17BalancesChanged.ContainsKey(toKey)) nep17BalancesChanged.Add(toKey, new Nep17Balance());
            }
            if (scriptContainer is Transaction transaction)
            {
                RecordTransferHistory(snapshot, scriptHash, from, to, amountItem.GetInteger(), transaction.Hash, ref transferIndex);
            }
        }

        public void OnPersist(StoreView snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            // Start freshly with a new DBCache for each block.
            ResetBatch();
            Dictionary<Nep17BalanceKey, Nep17Balance> nep17BalancesChanged = new Dictionary<Nep17BalanceKey, Nep17Balance>();

            ushort transferIndex = 0;
            foreach (Blockchain.ApplicationExecuted appExecuted in applicationExecutedList)
            {
                // Executions that fault won't modify storage, so we can skip them.
                if (appExecuted.VMState.HasFlag(VMState.FAULT)) continue;
                foreach (var notifyEventArgs in appExecuted.Notifications)
                {
                    if (!(notifyEventArgs?.State is VM.Types.Array stateItems) || stateItems.Count == 0)
                        continue;
                    HandleNotification(snapshot, notifyEventArgs.ScriptContainer, notifyEventArgs.ScriptHash, notifyEventArgs.EventName,
                        stateItems, nep17BalancesChanged, ref transferIndex);
                }
            }

            foreach (var nep17BalancePair in nep17BalancesChanged)
            {
                // get guarantee accurate balances by calling balanceOf for keys that changed.
                byte[] script;
                using (ScriptBuilder sb = new ScriptBuilder())
                {
                    sb.EmitAppCall(nep17BalancePair.Key.AssetScriptHash, "balanceOf",
                        nep17BalancePair.Key.UserScriptHash.ToArray());
                    script = sb.ToArray();
                }

                using (ApplicationEngine engine = ApplicationEngine.Run(script, snapshot, gas: 100000000))
                {
                    if (engine.State.HasFlag(VMState.FAULT)) continue;
                    if (engine.ResultStack.Count <= 0) continue;
                    nep17BalancePair.Value.Balance = engine.ResultStack.Pop().GetInteger();
                }
                nep17BalancePair.Value.LastUpdatedBlock = snapshot.Height;
                if (nep17BalancePair.Value.Balance == 0)
                {
                    _balances.Delete(nep17BalancePair.Key);
                    continue;
                }
                var itemToChange = _balances.GetAndChange(nep17BalancePair.Key, () => nep17BalancePair.Value);
                if (itemToChange != nep17BalancePair.Value)
                    itemToChange.FromReplica(nep17BalancePair.Value);
            }
        }

        public void OnCommit(StoreView snapshot)
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

        private void AddTransfers(byte dbPrefix, UInt160 userScriptHash, ulong startTime, ulong endTime,
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

            var transferPairs = _db.FindRange<Nep17TransferKey, Nep17Transfer>(
                prefix.Concat(startTimeBytes).ToArray(),
                prefix.Concat(endTimeBytes).ToArray());

            int resultCount = 0;
            foreach (var (key, value) in transferPairs)
            {
                if (++resultCount > _maxResults) break;
                JObject transfer = new JObject();
                transfer["timestamp"] = key.TimestampMS;
                transfer["assethash"] = key.AssetScriptHash.ToString();
                transfer["transferaddress"] = value.UserScriptHash == UInt160.Zero ? null : value.UserScriptHash.ToAddress();
                transfer["amount"] = value.Amount.ToString();
                transfer["blockindex"] = value.BlockIndex;
                transfer["transfernotifyindex"] = key.BlockXferNotificationIndex;
                transfer["txhash"] = value.TxHash.ToString();
                parentJArray.Add(transfer);
            }
        }

        private UInt160 GetScriptHashFromParam(string addressOrScriptHash)
        {
            return addressOrScriptHash.Length < 40 ?
                addressOrScriptHash.ToScriptHash() : UInt160.Parse(addressOrScriptHash);
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
            JArray transfersSent = new JArray();
            json["sent"] = transfersSent;
            JArray transfersReceived = new JArray();
            json["received"] = transfersReceived;
            json["address"] = userScriptHash.ToAddress();
            AddTransfers(Nep17TransferSentPrefix, userScriptHash, startTime, endTime, transfersSent);
            AddTransfers(Nep17TransferReceivedPrefix, userScriptHash, startTime, endTime, transfersReceived);
            return json;
        }

        [RpcMethod]
        public JObject GetNep17Balances(JArray _params)
        {
            using SnapshotView snapshot = Blockchain.Singleton.GetSnapshot();
            UInt160 userScriptHash = GetScriptHashFromParam(_params[0].AsString());

            JObject json = new JObject();
            JArray balances = new JArray();
            json["balance"] = balances;
            json["address"] = userScriptHash.ToAddress();
            var dbCache = new DbCache<Nep17BalanceKey, Nep17Balance>(_db, null, null, Nep17BalancePrefix);
            byte[] prefix = userScriptHash.ToArray();
            foreach (var (key, value) in dbCache.Find(prefix))
            {
                JObject balance = new JObject();
                if (NativeContract.ContractManagement.GetContract(snapshot, key.AssetScriptHash) is null)
                    continue;
                balance["assethash"] = key.AssetScriptHash.ToString();
                balance["amount"] = value.Balance.ToString();
                balance["lastupdatedblock"] = value.LastUpdatedBlock;
                balances.Add(balance);
            }
            return json;
        }
    }
}
