// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Network.RPC is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.IO;
using Neo.Json;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.VM.Types;

namespace Neo.Plugins
{
    partial class NeoService
    {
        [ServiceMethod]
        protected virtual JToken GetBestBlockHash(JArray _params)
        {
            return NativeContract.Ledger.CurrentHash(System.StoreView).ToString();
        }

        [ServiceMethod]
        protected virtual JToken GetBlock(JArray _params)
        {
            JToken key = _params[0];
            bool verbose = _params.Count >= 2 && _params[1].AsBoolean();
            using var snapshot = System.GetSnapshot();
            Block block;
            if (key is JNumber)
            {
                uint index = uint.Parse(key.AsString());
                block = NativeContract.Ledger.GetBlock(snapshot, index);
            }
            else
            {
                UInt256 hash = UInt256.Parse(key.AsString());
                block = NativeContract.Ledger.GetBlock(snapshot, hash);
            }
            if (block == null)
                throw new ServicceException(-100, "Unknown block");
            if (verbose)
            {
                JObject json = Utility.BlockToJson(block, System.Settings);
                json["confirmations"] = NativeContract.Ledger.CurrentIndex(snapshot) - block.Index + 1;
                UInt256 hash = NativeContract.Ledger.GetBlockHash(snapshot, block.Index + 1);
                if (hash != null)
                    json["nextblockhash"] = hash.ToString();
                return json;
            }
            return Convert.ToBase64String(block.ToArray());
        }

        [ServiceMethod]
        protected virtual JToken GetBlockHeaderCount(JArray _params)
        {
            return (System.HeaderCache.Last?.Index ?? NativeContract.Ledger.CurrentIndex(System.StoreView)) + 1;
        }

        [ServiceMethod]
        protected virtual JToken GetBlockCount(JArray _params)
        {
            return NativeContract.Ledger.CurrentIndex(System.StoreView) + 1;
        }

        [ServiceMethod]
        protected virtual JToken GetBlockHash(JArray _params)
        {
            uint height = uint.Parse(_params[0].AsString());
            var snapshot = System.StoreView;
            if (height <= NativeContract.Ledger.CurrentIndex(snapshot))
            {
                return NativeContract.Ledger.GetBlockHash(snapshot, height).ToString();
            }
            throw new ServicceException(-100, "Invalid Height");
        }

        [ServiceMethod]
        protected virtual JToken GetBlockHeader(JArray _params)
        {
            JToken key = _params[0];
            bool verbose = _params.Count >= 2 && _params[1].AsBoolean();
            var snapshot = System.StoreView;
            Header header;
            if (key is JNumber)
            {
                uint height = uint.Parse(key.AsString());
                header = NativeContract.Ledger.GetHeader(snapshot, height);
            }
            else
            {
                UInt256 hash = UInt256.Parse(key.AsString());
                header = NativeContract.Ledger.GetHeader(snapshot, hash);
            }
            if (header == null)
                throw new ServicceException(-100, "Unknown block");

            if (verbose)
            {
                JObject json = header.ToJson(System.Settings);
                json["confirmations"] = NativeContract.Ledger.CurrentIndex(snapshot) - header.Index + 1;
                UInt256 hash = NativeContract.Ledger.GetBlockHash(snapshot, header.Index + 1);
                if (hash != null)
                    json["nextblockhash"] = hash.ToString();
                return json;
            }

            return Convert.ToBase64String(header.ToArray());
        }

        [ServiceMethod]
        protected virtual JToken GetContractState(JArray _params)
        {
            if (int.TryParse(_params[0].AsString(), out int contractId))
            {
                var contracts = NativeContract.ContractManagement.GetContractById(System.StoreView, contractId);
                return contracts?.ToJson() ?? throw new ServicceException(-100, "Unknown contract");
            }
            else
            {
                UInt160 script_hash = ToScriptHash(_params[0].AsString());
                ContractState contract = NativeContract.ContractManagement.GetContract(System.StoreView, script_hash);
                return contract?.ToJson() ?? throw new ServicceException(-100, "Unknown contract");
            }
        }

        private static UInt160 ToScriptHash(string keyword)
        {
            foreach (var native in NativeContract.Contracts)
            {
                if (keyword.Equals(native.Name, StringComparison.InvariantCultureIgnoreCase) || keyword == native.Id.ToString())
                    return native.Hash;
            }

            return UInt160.Parse(keyword);
        }

        [ServiceMethod]
        protected virtual JToken GetRawMemPool(JArray _params)
        {
            bool shouldGetUnverified = _params.Count >= 1 && _params[0].AsBoolean();
            if (!shouldGetUnverified)
                return new JArray(System.MemPool.GetVerifiedTransactions().Select(p => (JToken)p.Hash.ToString()));

            JObject json = new();
            json["height"] = NativeContract.Ledger.CurrentIndex(System.StoreView);
            System.MemPool.GetVerifiedAndUnverifiedTransactions(
                out IEnumerable<Transaction> verifiedTransactions,
                out IEnumerable<Transaction> unverifiedTransactions);
            json["verified"] = new JArray(verifiedTransactions.Select(p => (JToken)p.Hash.ToString()));
            json["unverified"] = new JArray(unverifiedTransactions.Select(p => (JToken)p.Hash.ToString()));
            return json;
        }

        [ServiceMethod]
        protected virtual JToken GetRawTransaction(JArray _params)
        {
            UInt256 hash = UInt256.Parse(_params[0].AsString());
            bool verbose = _params.Count >= 2 && _params[1].AsBoolean();
            if (System.MemPool.TryGetValue(hash, out Transaction tx) && !verbose)
                return Convert.ToBase64String(tx.ToArray());
            var snapshot = System.StoreView;
            TransactionState state = NativeContract.Ledger.GetTransactionState(snapshot, hash);
            tx ??= state?.Transaction;
            if (tx is null) throw new ServicceException(-100, "Unknown transaction");
            if (!verbose) return Convert.ToBase64String(tx.ToArray());
            JObject json = Utility.TransactionToJson(tx, System.Settings);
            if (state is not null)
            {
                TrimmedBlock block = NativeContract.Ledger.GetTrimmedBlock(snapshot, NativeContract.Ledger.GetBlockHash(snapshot, state.BlockIndex));
                json["blockhash"] = block.Hash.ToString();
                json["confirmations"] = NativeContract.Ledger.CurrentIndex(snapshot) - block.Index + 1;
                json["blocktime"] = block.Header.Timestamp;
            }
            return json;
        }

        [ServiceMethod]
        protected virtual JToken GetStorage(JArray _params)
        {
            using var snapshot = System.GetSnapshot();
            if (!int.TryParse(_params[0].AsString(), out int id))
            {
                UInt160 hash = UInt160.Parse(_params[0].AsString());
                ContractState contract = NativeContract.ContractManagement.GetContract(snapshot, hash);
                if (contract is null) throw new ServicceException(-100, "Unknown contract");
                id = contract.Id;
            }
            byte[] key = Convert.FromBase64String(_params[1].AsString());
            StorageItem item = snapshot.TryGet(new StorageKey
            {
                Id = id,
                Key = key
            });
            if (item is null) throw new ServicceException(-100, "Unknown storage");
            return Convert.ToBase64String(item.Value.Span);
        }

        [ServiceMethod]
        protected virtual JToken FindStorage(JArray _params)
        {
            using var snapshot = System.GetSnapshot();
            if (!int.TryParse(_params[0].AsString(), out int id))
            {
                UInt160 hash = UInt160.Parse(_params[0].AsString());
                ContractState contract = NativeContract.ContractManagement.GetContract(snapshot, hash);
                if (contract is null) throw new ServicceException(-100, "Unknown contract");
                id = contract.Id;
            }

            byte[] prefix = Convert.FromBase64String(_params[1].AsString());
            byte[] prefix_key = StorageKey.CreateSearchPrefix(id, prefix);

            if (!int.TryParse(_params[2].AsString(), out int start))
            {
                start = 0;
            }

            JObject json = new();
            JArray jarr = new();
            int pageSize = Settings.FindStoragePageSize;
            int i = 0;

            using (var iter = snapshot.Find(prefix_key).Skip(count: start).GetEnumerator())
            {
                var hasMore = false;
                while (iter.MoveNext())
                {
                    if (i == pageSize)
                    {
                        hasMore = true;
                        break;
                    }

                    JObject j = new();
                    j["key"] = Convert.ToBase64String(iter.Current.Key.Key.Span);
                    j["value"] = Convert.ToBase64String(iter.Current.Value.Value.Span);
                    jarr.Add(j);
                    i++;
                }
                json["truncated"] = hasMore;
            }

            json["next"] = start + i;
            json["results"] = jarr;
            return json;
        }

        [ServiceMethod]
        protected virtual JToken GetTransactionHeight(JArray _params)
        {
            UInt256 hash = UInt256.Parse(_params[0].AsString());
            uint? height = NativeContract.Ledger.GetTransactionState(System.StoreView, hash)?.BlockIndex;
            if (height.HasValue) return height.Value;
            throw new ServicceException(-100, "Unknown transaction");
        }

        [ServiceMethod]
        protected virtual JToken GetNextBlockValidators(JArray _params)
        {
            using var snapshot = System.GetSnapshot();
            var validators = NativeContract.NEO.GetNextBlockValidators(snapshot, System.Settings.ValidatorsCount);
            return validators.Select(p =>
            {
                JObject validator = new();
                validator["publickey"] = p.ToString();
                validator["votes"] = (int)NativeContract.NEO.GetCandidateVote(snapshot, p);
                return validator;
            }).ToArray();
        }

        [ServiceMethod]
        protected virtual JToken GetCandidates(JArray _params)
        {
            using var snapshot = System.GetSnapshot();
            byte[] script;
            using (ScriptBuilder sb = new())
            {
                script = sb.EmitDynamicCall(NativeContract.NEO.Hash, "getCandidates", null).ToArray();
            }
            using ApplicationEngine engine = ApplicationEngine.Run(script, snapshot, settings: System.Settings, gas: Settings.MaxGasInvoke);
            JObject json = new();
            try
            {
                var resultstack = engine.ResultStack.ToArray();
                if (resultstack.Length > 0)
                {
                    JArray jArray = new();
                    var validators = NativeContract.NEO.GetNextBlockValidators(snapshot, System.Settings.ValidatorsCount);

                    foreach (var item in resultstack)
                    {
                        var value = (VM.Types.Array)item;
                        foreach (Struct ele in value)
                        {
                            var publickey = ele[0].GetSpan().ToHexString();
                            json["publickey"] = publickey;
                            json["votes"] = ele[1].GetInteger().ToString();
                            json["active"] = validators.ToByteArray().ToHexString().Contains(publickey);
                            jArray.Add(json);
                            json = new();
                        }
                        return jArray;
                    }
                }
            }
            catch (InvalidOperationException)
            {
                json["exception"] = "Invalid result.";
            }
            return json;
        }

        [ServiceMethod]
        protected virtual JToken GetCommittee(JArray _params)
        {
            return new JArray(NativeContract.NEO.GetCommittee(System.StoreView).Select(p => (JToken)p.ToString()));
        }

        [ServiceMethod]
        protected virtual JToken GetNativeContracts(JArray _params)
        {
            return new JArray(NativeContract.Contracts.Select(p => p.NativeContractToJson(System.Settings)));
        }
    }
}
