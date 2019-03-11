using Microsoft.AspNetCore.Http;
using Neo.IO.Caching;
using Neo.IO.Data.LevelDB;
using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
using Neo.Persistence.LevelDB;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;
using Neo.Ledger;
using Snapshot = Neo.Persistence.Snapshot;

namespace Neo.Plugins
{
    public class RpcSystemAssetTrackerPlugin : Plugin, IPersistencePlugin, IRpcPlugin
    {
        private const byte SystemAssetUnspentCoinsPrefix = 0xfb;
        private DB _db;
        private DataCache<UserUnspentCoinOutputsKey, UserUnspentCoinOutputs> _userUnspentCoins;
        private WriteBatch _writeBatch;
        private int _rpcMaxUnspents;

        public override void Configure()
        {
            if (_db == null)
            {
                var dbPath = GetConfiguration().GetSection("DBPath").Value ?? "SystemAssetBalanceData";
                _db = DB.Open(dbPath, new Options { CreateIfMissing = true });
                _rpcMaxUnspents = int.Parse(GetConfiguration().GetSection("MaxReturnedUnspents").Value ?? "0");

            }
        }

        private void ResetBatch()
        {
            _writeBatch = new WriteBatch();
            var balancesSnapshot = _db.GetSnapshot();
            ReadOptions dbOptions = new ReadOptions { FillCache = false, Snapshot = balancesSnapshot };
            _userUnspentCoins = new DbCache<UserUnspentCoinOutputsKey, UserUnspentCoinOutputs>(_db, dbOptions,
                _writeBatch, SystemAssetUnspentCoinsPrefix);
        }

        public void OnPersist(Snapshot snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            // Start freshly with a new DBCache for each block.
            ResetBatch();

            foreach (Transaction tx in snapshot.PersistingBlock.Transactions)
            {
                ushort outputIndex = 0;
                foreach (TransactionOutput output in tx.Outputs)
                {
                    bool isGoverningToken = output.AssetId.Equals(Blockchain.GoverningToken.Hash);
                    if (isGoverningToken || output.AssetId.Equals(Blockchain.UtilityToken.Hash))
                    {
                        // Add new unspent UTXOs by account script hash.
                        UserUnspentCoinOutputs outputs = _userUnspentCoins.GetAndChange(
                            new UserUnspentCoinOutputsKey(isGoverningToken, output.ScriptHash, tx.Hash),
                            () => new UserUnspentCoinOutputs());
                        outputs.AddTxIndex(outputIndex, output.Value);
                    }
                    outputIndex++;
                }

                // Iterate all input Transactions by grouping by common input hashes.
                foreach (var group in tx.Inputs.GroupBy(p => p.PrevHash))
                {
                    TransactionState txPrev = snapshot.Transactions[group.Key];
                    // For each input being spent by this transaction.
                    foreach (CoinReference input in group)
                    {
                        // Get the output from the the previous transaction that is now being spent.
                        var outPrev = txPrev.Transaction.Outputs[input.PrevIndex];

                        bool isGoverningToken = outPrev.AssetId.Equals(Blockchain.GoverningToken.Hash);
                        if (isGoverningToken || outPrev.AssetId.Equals(Blockchain.UtilityToken.Hash))
                        {
                            // Remove spent UTXOs for unspent outputs by account script hash.
                            var userUnspentCoinOutputsKey =
                                new UserUnspentCoinOutputsKey(isGoverningToken, outPrev.ScriptHash, input.PrevHash);
                            UserUnspentCoinOutputs outputs = _userUnspentCoins.GetAndChange(
                                userUnspentCoinOutputsKey, () => new UserUnspentCoinOutputs());
                            outputs.RemoveTxIndex(input.PrevIndex);
                            if (outputs.AmountByTxIndex.Count == 0)
                                _userUnspentCoins.Delete(userUnspentCoinOutputsKey);
                        }
                    }
                }
            }
        }

        public void OnCommit(Snapshot snapshot)
        {
            _userUnspentCoins.Commit();
            _db.Write(WriteOptions.Default, _writeBatch);
        }

        public bool ShouldThrowExceptionFromCommit(Exception ex)
        {
            return true;
        }

        public void PreProcess(HttpContext context, string method, JArray _params)
        {
        }

        private UInt160 GetScriptHashFromParam(string addressOrScriptHash)
        {
            return addressOrScriptHash.Length < 40 ?
                addressOrScriptHash.ToScriptHash() : UInt160.Parse(addressOrScriptHash);
        }

        public JObject OnProcess(HttpContext context, string method, JArray _params)
        {
            if (method != "getunspents") return null;

            UInt160 scriptHash = GetScriptHashFromParam(_params[0].AsString());

            string[] nativeAssetNames = {"GAS", "NEO"};
            UInt256[] nativeAssetIds = {Blockchain.UtilityToken.Hash, Blockchain.GoverningToken.Hash};

            byte startingToken = 0; // 0 = Utility Token (GAS), 1 = Governing Token (NEO)
            int maxIterations = 2;

            if (_params.Count > 1)
            {
                maxIterations = 1;
                bool isGoverningToken = _params[1].AsBoolean();
                if (isGoverningToken) startingToken = 1;
            }

            var unspentsCache = new DbCache<UserUnspentCoinOutputsKey, UserUnspentCoinOutputs>(
                _db, null, null, SystemAssetUnspentCoinsPrefix);

            (JArray, Fixed8) RetreiveUnspentsForPrefix(byte[] prefix)
            {
                var unspents = new JArray();
                Fixed8 total = new Fixed8(0);

                foreach (var unspentInTx in unspentsCache.Find(prefix))
                {
                    var txId = unspentInTx.Key.TxHash.ToString().Substring(2);
                    foreach (var unspent in unspentInTx.Value.AmountByTxIndex)
                    {
                        var utxo = new JObject();
                        utxo["txid"] = txId;
                        utxo["n"] = unspent.Key;
                        utxo["value"] = (double) (decimal) unspent.Value;
                        total += unspent.Value;

                        unspents.Add(utxo);
                        if (unspents.Count > _rpcMaxUnspents)
                            return (unspents, total);
                    }
                }
                return (unspents, total);
            }

            JObject json = new JObject();
            JArray balances = new JArray();
            json["balance"] = balances;
            json["address"] = scriptHash.ToAddress();
            for (byte tokenIndex = startingToken; maxIterations-- > 0; tokenIndex++)
            {
                byte[] prefix = new [] { tokenIndex }.Concat(scriptHash.ToArray()).ToArray();

                var (unspents, total) = RetreiveUnspentsForPrefix(prefix);

                if (unspents.Count <= 0) continue;

                var balance = new JObject();
                balance["unspent"] = unspents;
                balance["asset_hash"] = nativeAssetIds[tokenIndex].ToString().Substring(2);
                balance["asset_symbol"] = balance["asset"] = nativeAssetNames[tokenIndex];
                balance["amount"] = new JNumber((double) (decimal) total); ;
                balances.Add(balance);
            }

            return json;
        }

        public void PostProcess(HttpContext context, string method, JArray _params, JObject result)
        {
        }
    }
}
