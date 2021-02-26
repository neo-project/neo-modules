#pragma warning disable IDE0051
#pragma warning disable IDE0060

using Neo.IO;
using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Plugins
{
    partial class RpcServer
    {
        [RpcMethod]
        protected virtual JObject GetBestBlockHash(JArray _params)
        {
            return NativeContract.Ledger.CurrentHash(system.StoreView).ToString();
        }

        [RpcMethod]
        protected virtual JObject GetBlock(JArray _params)
        {
            JObject key = _params[0];
            bool verbose = _params.Count >= 2 && _params[1].AsBoolean();
            using var snapshot = system.GetSnapshot();
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
                throw new RpcException(-100, "Unknown block");
            if (verbose)
            {
                JObject json = Utility.BlockToJson(block, system.Settings);
                json["confirmations"] = NativeContract.Ledger.CurrentIndex(snapshot) - block.Index + 1;
                UInt256 hash = NativeContract.Ledger.GetBlockHash(snapshot, block.Index + 1);
                if (hash != null)
                    json["nextblockhash"] = hash.ToString();
                return json;
            }
            return Convert.ToBase64String(block.ToArray());
        }

        [RpcMethod]
        protected virtual JObject GetBlockHeaderCount(JArray _params)
        {
            return (system.HeaderCache.Last?.Index ?? NativeContract.Ledger.CurrentIndex(system.StoreView)) + 1;
        }

        [RpcMethod]
        protected virtual JObject GetBlockCount(JArray _params)
        {
            return NativeContract.Ledger.CurrentIndex(system.StoreView) + 1;
        }

        [RpcMethod]
        protected virtual JObject GetBlockHash(JArray _params)
        {
            uint height = uint.Parse(_params[0].AsString());
            var snapshot = system.StoreView;
            if (height <= NativeContract.Ledger.CurrentIndex(snapshot))
            {
                return NativeContract.Ledger.GetBlockHash(snapshot, height).ToString();
            }
            throw new RpcException(-100, "Invalid Height");
        }

        [RpcMethod]
        protected virtual JObject GetBlockHeader(JArray _params)
        {
            JObject key = _params[0];
            bool verbose = _params.Count >= 2 && _params[1].AsBoolean();
            var snapshot = system.StoreView;
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
                throw new RpcException(-100, "Unknown block");

            if (verbose)
            {
                JObject json = header.ToJson(system.Settings);
                json["confirmations"] = NativeContract.Ledger.CurrentIndex(snapshot) - header.Index + 1;
                UInt256 hash = NativeContract.Ledger.GetBlockHash(snapshot, header.Index + 1);
                if (hash != null)
                    json["nextblockhash"] = hash.ToString();
                return json;
            }

            return Convert.ToBase64String(header.ToArray());
        }

        [RpcMethod]
        protected virtual JObject GetContractState(JArray _params)
        {
            UInt160 script_hash = ToScriptHash(_params[0].AsString());
            ContractState contract = NativeContract.ContractManagement.GetContract(system.StoreView, script_hash);
            return contract?.ToJson() ?? throw new RpcException(-100, "Unknown contract");
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

        [RpcMethod]
        protected virtual JObject GetRawMemPool(JArray _params)
        {
            bool shouldGetUnverified = _params.Count >= 1 && _params[0].AsBoolean();
            if (!shouldGetUnverified)
                return new JArray(system.MemPool.GetVerifiedTransactions().Select(p => (JObject)p.Hash.ToString()));

            JObject json = new JObject();
            json["height"] = NativeContract.Ledger.CurrentIndex(system.StoreView);
            system.MemPool.GetVerifiedAndUnverifiedTransactions(
                out IEnumerable<Transaction> verifiedTransactions,
                out IEnumerable<Transaction> unverifiedTransactions);
            json["verified"] = new JArray(verifiedTransactions.Select(p => (JObject)p.Hash.ToString()));
            json["unverified"] = new JArray(unverifiedTransactions.Select(p => (JObject)p.Hash.ToString()));
            return json;
        }

        [RpcMethod]
        protected virtual JObject GetRawTransaction(JArray _params)
        {
            UInt256 hash = UInt256.Parse(_params[0].AsString());
            bool verbose = _params.Count >= 2 && _params[1].AsBoolean();
            if (system.MemPool.TryGetValue(hash, out Transaction tx) && !verbose)
                return Convert.ToBase64String(tx.ToArray());
            var snapshot = system.StoreView;
            TransactionState state = NativeContract.Ledger.GetTransactionState(snapshot, hash);
            tx ??= state?.Transaction;
            if (tx is null) throw new RpcException(-100, "Unknown transaction");
            if (!verbose) return Convert.ToBase64String(tx.ToArray());
            JObject json = Utility.TransactionToJson(tx, system.Settings);
            if (state is not null)
            {
                TrimmedBlock block = NativeContract.Ledger.GetTrimmedBlock(snapshot, NativeContract.Ledger.GetBlockHash(snapshot, state.BlockIndex));
                json["blockhash"] = block.Hash.ToString();
                json["confirmations"] = NativeContract.Ledger.CurrentIndex(snapshot) - block.Index + 1;
                json["blocktime"] = block.Header.Timestamp;
            }
            return json;
        }

        [RpcMethod]
        protected virtual JObject GetStorage(JArray _params)
        {
            using var snapshot = system.GetSnapshot();
            if (!int.TryParse(_params[0].AsString(), out int id))
            {
                UInt160 hash = UInt160.Parse(_params[0].AsString());
                ContractState contract = NativeContract.ContractManagement.GetContract(snapshot, hash);
                if (contract is null) throw new RpcException(-100, "Unknown contract");
                id = contract.Id;
            }
            byte[] key = Convert.FromBase64String(_params[1].AsString());
            StorageItem item = snapshot.TryGet(new StorageKey
            {
                Id = id,
                Key = key
            });
            if (item is null) throw new RpcException(-100, "Unknown storage");
            return Convert.ToBase64String(item.Value);
        }

        [RpcMethod]
        protected virtual JObject GetTransactionHeight(JArray _params)
        {
            UInt256 hash = UInt256.Parse(_params[0].AsString());
            uint? height = NativeContract.Ledger.GetTransactionState(system.StoreView, hash)?.BlockIndex;
            if (height.HasValue) return height.Value;
            throw new RpcException(-100, "Unknown transaction");
        }

        [RpcMethod]
        protected virtual JObject GetNextBlockValidators(JArray _params)
        {
            using var snapshot = system.GetSnapshot();
            var validators = NativeContract.NEO.GetNextBlockValidators(snapshot, system.Settings.ValidatorsCount);
            var candidates = NativeContract.NEO.GetCandidates(snapshot);
            if (candidates.Length > 0)
            {
                return candidates.Select(p =>
                {
                    JObject validator = new JObject();
                    validator["publickey"] = p.PublicKey.ToString();
                    validator["votes"] = p.Votes.ToString();
                    validator["active"] = validators.Contains(p.PublicKey);
                    return validator;
                }).ToArray();
            }
            else
            {
                return validators.Select(p =>
                {
                    JObject validator = new JObject();
                    validator["publickey"] = p.ToString();
                    validator["votes"] = 0;
                    validator["active"] = true;
                    return validator;
                }).ToArray();
            }
        }

        [RpcMethod]
        protected virtual JObject GetCommittee(JArray _params)
        {
            return new JArray(NativeContract.NEO.GetCommittee(system.StoreView).Select(p => (JObject)p.ToString()));
        }

        [RpcMethod]
        protected virtual JObject GetNativeContracts(JArray _params)
        {
            return new JArray(NativeContract.Contracts.Select(p => p.NativeContractToJson(system.Settings)));
        }
    }
}
