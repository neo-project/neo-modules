#pragma warning disable IDE0051
#pragma warning disable IDE0060

using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
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
            return Blockchain.Singleton.CurrentBlockHash.ToString();
        }

        [RpcMethod]
        protected virtual JObject GetBlock(JArray _params)
        {
            JObject key = _params[0];
            bool verbose = _params.Count >= 2 && _params[1].AsBoolean();
            Block block;
            if (key is JNumber)
            {
                uint index = uint.Parse(key.AsString());
                block = Blockchain.Singleton.GetBlock(index);
            }
            else
            {
                UInt256 hash = UInt256.Parse(key.AsString());
                block = Blockchain.Singleton.View.GetBlock(hash);
            }
            if (block == null)
                throw new RpcException(-100, "Unknown block");
            if (verbose)
            {
                JObject json = block.ToJson();
                json["confirmations"] = Blockchain.Singleton.Height - block.Index + 1;
                UInt256 hash = Blockchain.Singleton.GetNextBlockHash(block.Hash);
                if (hash != null)
                    json["nextblockhash"] = hash.ToString();
                return json;
            }
            return Convert.ToBase64String(block.ToArray());
        }

        [RpcMethod]
        protected virtual JObject GetBlockCount(JArray _params)
        {
            return Blockchain.Singleton.Height + 1;
        }

        [RpcMethod]
        protected virtual JObject GetBlockHash(JArray _params)
        {
            uint height = uint.Parse(_params[0].AsString());
            if (height <= Blockchain.Singleton.Height)
            {
                return Blockchain.Singleton.GetBlockHash(height).ToString();
            }
            throw new RpcException(-100, "Invalid Height");
        }

        [RpcMethod]
        protected virtual JObject GetBlockHeader(JArray _params)
        {
            JObject key = _params[0];
            bool verbose = _params.Count >= 2 && _params[1].AsBoolean();
            Header header;
            if (key is JNumber)
            {
                uint height = uint.Parse(key.AsString());
                header = Blockchain.Singleton.GetHeader(height);
            }
            else
            {
                UInt256 hash = UInt256.Parse(key.AsString());
                header = Blockchain.Singleton.View.GetHeader(hash);
            }
            if (header == null)
                throw new RpcException(-100, "Unknown block");

            if (verbose)
            {
                JObject json = header.ToJson();
                json["confirmations"] = Blockchain.Singleton.Height - header.Index + 1;
                UInt256 hash = Blockchain.Singleton.GetNextBlockHash(header.Hash);
                if (hash != null)
                    json["nextblockhash"] = hash.ToString();
                return json;
            }

            return Convert.ToBase64String(header.ToArray());
        }

        [RpcMethod]
        protected virtual JObject GetContractState(JArray _params)
        {
            UInt160 script_hash = UInt160.Parse(_params[0].AsString());
            ContractState contract = Blockchain.Singleton.View.Contracts.TryGet(script_hash);
            return contract?.ToJson() ?? throw new RpcException(-100, "Unknown contract");
        }

        [RpcMethod]
        protected virtual JObject GetRawMemPool(JArray _params)
        {
            bool shouldGetUnverified = _params.Count >= 1 && _params[0].AsBoolean();
            if (!shouldGetUnverified)
                return new JArray(Blockchain.Singleton.MemPool.GetVerifiedTransactions().Select(p => (JObject)p.Hash.ToString()));

            JObject json = new JObject();
            json["height"] = Blockchain.Singleton.Height;
            Blockchain.Singleton.MemPool.GetVerifiedAndUnverifiedTransactions(
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
            Transaction tx = Blockchain.Singleton.GetTransaction(hash);
            if (tx == null)
                throw new RpcException(-100, "Unknown transaction");
            if (verbose)
            {
                JObject json = tx.ToJson();
                TransactionState txState = Blockchain.Singleton.View.Transactions.TryGet(hash);
                if (txState != null)
                {
                    Header header = Blockchain.Singleton.GetHeader(txState.BlockIndex);
                    json["blockhash"] = header.Hash.ToString();
                    json["confirmations"] = Blockchain.Singleton.Height - header.Index + 1;
                    json["blocktime"] = header.Timestamp;
                    json["vmstate"] = txState.VMState;
                }
                return json;
            }
            return Convert.ToBase64String(tx.ToArray());
        }

        [RpcMethod]
        protected virtual JObject GetStorage(JArray _params)
        {
            if (!int.TryParse(_params[0].AsString(), out int id))
            {
                UInt160 script_hash = UInt160.Parse(_params[0].AsString());
                ContractState contract = Blockchain.Singleton.View.Contracts.TryGet(script_hash);
                if (contract == null) return null;
                id = contract.Id;
            }
            byte[] key = _params[1].AsString().HexToBytes();
            StorageItem item = Blockchain.Singleton.View.Storages.TryGet(new StorageKey
            {
                Id = id,
                Key = key
            }) ?? new StorageItem();
            return item.Value?.ToHexString();
        }

        [RpcMethod]
        protected virtual JObject GetTransactionHeight(JArray _params)
        {
            UInt256 hash = UInt256.Parse(_params[0].AsString());
            uint? height = Blockchain.Singleton.View.Transactions.TryGet(hash)?.BlockIndex;
            if (height.HasValue) return height.Value;
            throw new RpcException(-100, "Unknown transaction");
        }

        [RpcMethod]
        protected virtual JObject GetNextBlockValidators(JArray _params)
        {
            using SnapshotView snapshot = Blockchain.Singleton.GetSnapshot();
            var validators = NativeContract.NEO.GetNextBlockValidators(snapshot);
            return NativeContract.NEO.GetCandidates(snapshot).Select(p =>
            {
                JObject validator = new JObject();
                validator["publickey"] = p.PublicKey.ToString();
                validator["votes"] = p.Votes.ToString();
                validator["active"] = validators.Contains(p.PublicKey);
                return validator;
            }).ToArray();
        }

        [RpcMethod]
        protected virtual JObject GetCommittee(JArray _params)
        {
            using SnapshotView snapshot = Blockchain.Singleton.GetSnapshot();
            return new JArray(NativeContract.NEO.GetCommittee(snapshot).Select(p => (JObject)p.ToString()));
        }
    }
}
