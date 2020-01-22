#pragma warning disable IDE0051
#pragma warning disable IDE0060

using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Plugins
{
    partial class RpcServer
    {
        [RpcMethod]
        private JObject GetBestBlockHash(JArray _params)
        {
            return Blockchain.Singleton.CurrentBlockHash.ToString();
        }

        [RpcMethod]
        private JObject GetBlock(JArray _params)
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
                return new RpcBlock
                {
                    Block = block,
                    Confirmations = Blockchain.Singleton.Height - block.Index + 1,
                    NextBlockHash = Blockchain.Singleton.GetNextBlockHash(block.Hash)
                }.ToJson();
            }
            return block.ToArray().ToHexString();
        }

        [RpcMethod]
        private JObject GetBlockCount(JArray _params)
        {
            return Blockchain.Singleton.Height + 1;
        }

        [RpcMethod]
        private JObject GetBlockHash(JArray _params)
        {
            uint height = uint.Parse(_params[0].AsString());
            if (height <= Blockchain.Singleton.Height)
            {
                return Blockchain.Singleton.GetBlockHash(height).ToString();
            }
            throw new RpcException(-100, "Invalid Height");
        }

        [RpcMethod]
        private JObject GetBlockHeader(JArray _params)
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
                return new RpcBlockHeader
                {
                    Header = header,
                    Confirmations = Blockchain.Singleton.Height - header.Index + 1,
                    NextBlockHash = Blockchain.Singleton.GetNextBlockHash(header.Hash)
                }.ToJson();
            }

            return header.ToArray().ToHexString();
        }

        [RpcMethod]
        private JObject GetBlockSysFee(JArray _params)
        {
            uint height = uint.Parse(_params[0].AsString());
            if (height <= Blockchain.Singleton.Height)
                using (ApplicationEngine engine = NativeContract.GAS.TestCall("getSysFeeAmount", height))
                {
                    return engine.ResultStack.Peek().GetBigInteger().ToString();
                }
            throw new RpcException(-100, "Invalid Height");
        }

        [RpcMethod]
        private JObject GetContractState(JArray _params)
        {
            UInt160 script_hash = UInt160.Parse(_params[0].AsString());
            ContractState contract = Blockchain.Singleton.View.Contracts.TryGet(script_hash);
            return contract?.ToJson() ?? throw new RpcException(-100, "Unknown contract");
        }

        [RpcMethod]
        private JObject GetRawMemPool(JArray _params)
        {
            bool shouldGetUnverified = _params.Count >= 1 && _params[0].AsBoolean();
            if (!shouldGetUnverified)
                return new JArray(Blockchain.Singleton.MemPool.GetVerifiedTransactions().Select(p => (JObject)p.Hash.ToString()));
            Blockchain.Singleton.MemPool.GetVerifiedAndUnverifiedTransactions(
                out IEnumerable<Transaction> verifiedTransactions,
                out IEnumerable<Transaction> unverifiedTransactions);

            return new RpcRawMemPool
            {
                Height = Blockchain.Singleton.Height,
                Verified = verifiedTransactions.Select(p => p.Hash).ToList(),
                UnVerified = unverifiedTransactions.Select(p => p.Hash).ToList()
            }.ToJson();
        }

        [RpcMethod]
        private JObject GetRawTransaction(JArray _params)
        {
            UInt256 hash = UInt256.Parse(_params[0].AsString());
            bool verbose = _params.Count >= 2 && _params[1].AsBoolean();
            Transaction tx = Blockchain.Singleton.GetTransaction(hash);
            if (tx == null)
                throw new RpcException(-100, "Unknown transaction");
            if (verbose)
            {
                var result = new RpcTransaction { Transaction = tx };

                TransactionState txState = Blockchain.Singleton.View.Transactions.TryGet(hash);
                if (txState != null)
                {
                    Header header = Blockchain.Singleton.GetHeader(txState.BlockIndex);
                    result.BlockHash = header.Hash;
                    result.Confirmations = Blockchain.Singleton.Height - header.Index + 1;
                    result.BlockTime = header.Timestamp;
                    result.BlockHash = header.Hash;
                }
                return result.ToJson();
            }
            return tx.ToArray().ToHexString();
        }

        [RpcMethod]
        private JObject GetStorage(JArray _params)
        {
            UInt160 script_hash = UInt160.Parse(_params[0].AsString());
            byte[] key = _params[1].AsString().HexToBytes();
            StorageItem item = Blockchain.Singleton.View.Storages.TryGet(new StorageKey
            {
                ScriptHash = script_hash,
                Key = key
            }) ?? new StorageItem();
            return item.Value?.ToHexString();
        }

        [RpcMethod]
        private JObject GetTransactionHeight(JArray _params)
        {
            UInt256 hash = UInt256.Parse(_params[0].AsString());
            uint? height = Blockchain.Singleton.View.Transactions.TryGet(hash)?.BlockIndex;
            if (height.HasValue) return height.Value;
            throw new RpcException(-100, "Unknown transaction");
        }

        [RpcMethod]
        private JObject GetValidators(JArray _params)
        {
            using SnapshotView snapshot = Blockchain.Singleton.GetSnapshot();
            var validators = NativeContract.NEO.GetValidators(snapshot);
            return NativeContract.NEO.GetRegisteredValidators(snapshot).Select(p =>
            {
                return new RpcValidator
                {
                    PublicKey = p.PublicKey.ToString(),
                    Votes = p.Votes,
                    Active = validators.Contains(p.PublicKey)
                }.ToJson();
            }).ToArray();
        }
    }
}
