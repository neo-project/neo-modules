#pragma warning disable IDE0051
#pragma warning disable IDE0060

using Microsoft.AspNetCore.Mvc;
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
    partial class RestController
    {
        /// <summary>
        /// Get the lastest block hash of the blockchain 
        /// </summary>
        /// <returns></returns>
        [HttpGet("blocks/bestblockhash")]
        public IActionResult GetBestBlockHash()
        {
            return Ok(Blockchain.Singleton.CurrentBlockHash.ToString());
        }

        /// <summary>
        /// Get a block with the specified hash or at a certain height, only hash taking effect if hash and index are both non-null
        /// </summary>
        /// <param name="hash">block hash</param>
        /// <param name="index">block height</param>
        /// <param name="verbose">0:get block serialized in hexstring; 1: get block in Json format</param>
        /// <returns></returns>
        [HttpGet("blocks")]
        public IActionResult GetBlock(string hash, int index, int verbose = 0)
        {
            JObject key;
            if (hash != null)
            {
                key = new JString(hash);
            }
            else
            {
                key = new JNumber(index);
            }
            bool isVerbose = verbose == 0 ? false : true;
            Block block;
            if (key is JNumber)
            {
                uint _index = uint.Parse(key.AsString());
                block = Blockchain.Singleton.GetBlock(_index);
            }
            else
            {
                UInt256 _hash = UInt256.Parse(key.AsString());
                block = Blockchain.Singleton.View.GetBlock(_hash);
            }
            if (block == null)
                throw new RestException(-100, "Unknown block");
            if (isVerbose)
            {
                JObject json = block.ToJson();
                json["confirmations"] = Blockchain.Singleton.Height - block.Index + 1;
                UInt256 _hash = Blockchain.Singleton.GetNextBlockHash(block.Hash);
                if (hash != null)
                    json["nextblockhash"] = hash.ToString();
                return FormatJson(json);
            }
            return FormatJson(block.ToArray().ToHexString());
        }

        /// <summary>
        /// Get the block count of the blockchain
        /// </summary>
        /// <returns></returns>
        [HttpGet("blocks/count")]
        public IActionResult GetBlockCount()
        {
            return Ok(Blockchain.Singleton.Height + 1);
        }

        /// <summary>
        /// Get the block hash with the specified index
        /// </summary>
        /// <param name="index">block height</param>
        /// <returns></returns>
        [HttpGet("blocks/{index}/hash")]
        public IActionResult GetBlockHash(uint index = 0)
        {
            if (index <= Blockchain.Singleton.Height)
            {
                return Ok(Blockchain.Singleton.GetBlockHash(index).ToString());
            }
            throw new RestException(-100, "Invalid Height");
        }

        /// <summary>
        /// Get the block header with the specified hash or at a certain height, only hash taking effect if hash and index are both non-null
        /// </summary>
        /// <param name="hash">block hash</param>
        /// <param name="index">block height</param>
        /// <param name="verbose">0:get block serialized in hexstring; 1: get block in Json format</param>
        /// <returns></returns>
        [HttpGet("blocks/header")]
        public IActionResult GetBlockHeader(string hash, int index, int verbose = 0)
        {
            JObject key;
            if (hash != null)
            {
                key = new JString(hash);
            }
            else
            {
                key = new JNumber(index);
            }
            bool isVerbose = verbose == 0 ? false : true;
            Header header;
            if (key is JNumber)
            {
                uint height = uint.Parse(key.AsString());
                header = Blockchain.Singleton.GetHeader(height);
            }
            else
            {
                UInt256 _hash = UInt256.Parse(key.AsString());
                header = Blockchain.Singleton.View.GetHeader(_hash);
            }
            if (header == null)
                throw new RestException(-100, "Unknown block");

            if (isVerbose)
            {
                JObject json = header.ToJson();
                json["confirmations"] = Blockchain.Singleton.Height - header.Index + 1;
                UInt256 _hash = Blockchain.Singleton.GetNextBlockHash(header.Hash);
                if (_hash != null)
                    json["nextblockhash"] = _hash.ToString();
                return FormatJson(json);
            }

            return FormatJson(header.ToArray().ToHexString());
        }


        /// <summary>
        /// Get the system fees before the block with the specified index
        /// </summary>
        /// <param name="index">block height</param>
        /// <returns></returns>
        [HttpGet("blocks/{index}/sysfee")]
        public IActionResult GetBlockSysFee(uint index = 0)
        {
            if (index <= Blockchain.Singleton.Height)
                using (ApplicationEngine engine = NativeContract.GAS.TestCall("getSysFeeAmount", index))
                {
                    return Ok(engine.ResultStack.Peek().GetBigInteger().ToString());
                }
            throw new RestException(-100, "Invalid Height");
        }

        /// <summary>
        /// Get a contract with the specified script hash
        /// </summary>
        /// <param name="scriptHash">contract scriptHash</param>
        /// <returns></returns>
        [HttpGet("contracts/{scriptHash}")]
        public IActionResult GetContractState(string scriptHash)
        {
            UInt160 script_hash = UInt160.Parse(scriptHash);
            ContractState contract = Blockchain.Singleton.View.Contracts.TryGet(script_hash);
            if (contract != null)
            {
                return FormatJson(contract.ToJson());
            }
            throw new RestException(-100, "Unknown contract");
        }

        /// <summary>
        /// Gets unconfirmed transactions in memory
        /// </summary>
        /// <param name="getUnverified">0: get all transactions; 1: get verified transactions</param>
        /// <returns></returns>
        [HttpGet("network/localnode/rawmempool")]
        public IActionResult GetRawMemPool(int getUnverified = 0)
        {
            bool shouldGetUnverified = getUnverified == 0 ? false : true;
            if (!shouldGetUnverified)
                return FormatJson(new JArray(Blockchain.Singleton.MemPool.GetVerifiedTransactions().Select(p => (JObject)p.Hash.ToString())));

            JObject json = new JObject();
            json["height"] = Blockchain.Singleton.Height;
            Blockchain.Singleton.MemPool.GetVerifiedAndUnverifiedTransactions(
                out IEnumerable<Transaction> verifiedTransactions,
                out IEnumerable<Transaction> unverifiedTransactions);
            json["verified"] = new JArray(verifiedTransactions.Select(p => (JObject)p.Hash.ToString()));
            json["unverified"] = new JArray(unverifiedTransactions.Select(p => (JObject)p.Hash.ToString()));
            return FormatJson(json);
        }

        /// <summary>
        /// Get a transaction with the specified hash value	
        /// </summary>
        /// <param name="txid">transaction hash</param>
        /// <param name="verbose">0:get block serialized in hexstring; 1: get block in Json format</param>
        /// <returns></returns>
        [HttpGet("transactions/{txid}")]
        public IActionResult GetRawTransaction(string txid, int verbose = 0)
        {
            UInt256 hash = UInt256.Parse(txid);
            bool isVerbose = verbose == 0 ? false : true;
            Transaction tx = Blockchain.Singleton.GetTransaction(hash);
            if (tx == null)
                throw new RestException(-100, "Unknown transaction");
            if (isVerbose)
            {
                JObject json = tx.ToJson();
                TransactionState txState = Blockchain.Singleton.View.Transactions.TryGet(hash);
                if (txState != null)
                {
                    Header header = Blockchain.Singleton.GetHeader(txState.BlockIndex);
                    json["blockhash"] = header.Hash.ToString();
                    json["confirmations"] = Blockchain.Singleton.Height - header.Index + 1;
                    json["blocktime"] = header.Timestamp;
                    json["vmState"] = txState.VMState;
                }
                return FormatJson(json);
            }
            return FormatJson(tx.ToArray().ToHexString());
        }

        /// <summary>
        /// Get the stored value with the contract script hash and the key
        /// </summary>
        /// <param name="scriptHash">contract scriptHash</param>
        /// <param name="key">stored key</param>
        /// <returns></returns>
        [HttpGet("contracts/{scriptHash}/storage/{key}/value")]
        public IActionResult GetStorage(string scriptHash, string key)
        {
            UInt160 script_hash = UInt160.Parse(scriptHash);
            StorageItem item = Blockchain.Singleton.View.Storages.TryGet(new StorageKey
            {
                ScriptHash = script_hash,
                Key = key.HexToBytes()
            }) ?? new StorageItem();
            if(item.Value != null) return Ok(item.Value.ToHexString());
            throw new RestException(-100, "Key not exist");
        }

        /// <summary>
        /// Get the block index in which the transaction is found
        /// </summary>
        /// <param name="txid">transaction hash</param>
        /// <returns></returns>
        [HttpGet("transactions/{txid}/height")]
        public IActionResult GetTransactionHeight(string txid)
        {
            
            UInt256 hash = UInt256.Parse(txid);
            uint? height = Blockchain.Singleton.View.Transactions.TryGet(hash)?.BlockIndex;
            if (height.HasValue) return Ok(height.Value);
            throw new RestException(-100, "Unknown transaction");
        }

        /// <summary>
        /// Get latest validators
        /// </summary>
        /// <returns></returns>
        [HttpGet("validators/latest")]
        public IActionResult GetValidators()
        {
            JArray json = new JArray();
            using SnapshotView snapshot = Blockchain.Singleton.GetSnapshot();
            var validators = NativeContract.NEO.GetValidators(snapshot);
            json = NativeContract.NEO.GetRegisteredValidators(snapshot).Select(p =>
            {
                JObject validator = new JObject();
                validator["publickey"] = p.PublicKey.ToString();
                validator["votes"] = p.Votes.ToString();
                validator["active"] = validators.Contains(p.PublicKey);
                return validator;
            }).ToArray();
            return FormatJson(json);
        }

        private ContentResult FormatJson(JObject jObject)
        {
            return Content(jObject.ToString(), "application/json");
        }
    }
}
