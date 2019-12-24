using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Neo.Network.RPC
{
    /// <summary>
    /// The RPC client to call NEO RPC methods
    /// </summary>
    public class RpcClient : IDisposable
    {
        private readonly HttpClient httpClient;

        public RpcClient(string url)
        {
            httpClient = new HttpClient() { BaseAddress = new Uri(url) };
        }

        public RpcClient(HttpClient client)
        {
            httpClient = client;
        }

        public void Dispose()
        {
            httpClient?.Dispose();
        }

        public async Task<RpcResponse> SendAsync(RpcRequest request)
        {
            var requestJson = request.ToJson().ToString();
            using var result = await httpClient.PostAsync(httpClient.BaseAddress, new StringContent(requestJson, Encoding.UTF8));
            var content = await result.Content.ReadAsStringAsync();
            var response = RpcResponse.FromJson(JObject.Parse(content));
            response.RawResponse = content;

            if (response.Error != null)
            {
                throw new RpcException(response.Error.Code, response.Error.Message);
            }

            return response;
        }

        public RpcResponse Send(RpcRequest request)
        {
            try
            {
                return SendAsync(request).Result;
            }
            catch (AggregateException ex)
            {
                throw ex.GetBaseException();
            }
        }

        public virtual JObject RpcSend(string method, params JObject[] paraArgs)
        {
            var request = new RpcRequest
            {
                Id = 1,
                Jsonrpc = "2.0",
                Method = method,
                Params = paraArgs.Select(p => p).ToArray()
            };
            return Send(request).Result;
        }

        /// <summary>
        /// Returns the hash of the tallest block in the main chain.
        /// </summary>
        public string GetBestBlockHash()
        {
            return RpcSend("getbestblockhash").AsString();
        }

        /// <summary>
        /// Returns the hash of the tallest block in the main chain.
        /// The serialized information of the block is returned, represented by a hexadecimal string.
        /// </summary>
        public string GetBlockHex(string hashOrIndex)
        {
            if (int.TryParse(hashOrIndex, out int index))
            {
                return RpcSend("getblock", index).AsString();
            }
            return RpcSend("getblock", hashOrIndex).AsString();
        }

        /// <summary>
        /// Returns the hash of the tallest block in the main chain.
        /// </summary>
        public RpcBlock GetBlock(string hashOrIndex)
        {
            if (int.TryParse(hashOrIndex, out int index))
            {
                return RpcBlock.FromJson(RpcSend("getblock", index, true));
            }
            return RpcBlock.FromJson(RpcSend("getblock", hashOrIndex, true));
        }

        /// <summary>
        /// Gets the number of blocks in the main chain.
        /// </summary>
        public uint GetBlockCount()
        {
            return (uint)RpcSend("getblockcount").AsNumber();
        }

        /// <summary>
        /// Returns the hash value of the corresponding block, based on the specified index.
        /// </summary>
        public string GetBlockHash(int index)
        {
            return RpcSend("getblockhash", index).AsString();
        }

        /// <summary>
        /// Returns the corresponding block header information according to the specified script hash.
        /// </summary>
        public string GetBlockHeaderHex(string hashOrIndex)
        {
            if (int.TryParse(hashOrIndex, out int index))
            {
                return RpcSend("getblockheader", index).AsString();
            }
            return RpcSend("getblockheader", hashOrIndex).AsString();
        }

        /// <summary>
        /// Returns the corresponding block header information according to the specified script hash.
        /// </summary>
        public RpcBlockHeader GetBlockHeader(string hashOrIndex)
        {
            if (int.TryParse(hashOrIndex, out int index))
            {
                return RpcBlockHeader.FromJson(RpcSend("getblockheader", index, true));
            }
            return RpcBlockHeader.FromJson(RpcSend("getblockheader", hashOrIndex, true));
        }

        /// <summary>
        /// Returns the system fees of the block, based on the specified index.
        /// </summary>
        public string GetBlockSysFee(int height)
        {
            return RpcSend("getblocksysfee", height).AsString();
        }

        /// <summary>
        /// Gets the current number of connections for the node.
        /// </summary>
        public int GetConnectionCount()
        {
            return (int)RpcSend("getconnectioncount").AsNumber();
        }

        /// <summary>
        /// Queries contract information, according to the contract script hash.
        /// </summary>
        public ContractState GetContractState(string hash)
        {
            return ContractState.FromJson(RpcSend("getcontractstate", hash));
        }

        /// <summary>
        /// Gets the list of nodes that the node is currently connected/disconnected from.
        /// </summary>
        public RpcPeers GetPeers()
        {
            return RpcPeers.FromJson(RpcSend("getpeers"));
        }

        /// <summary>
        /// Obtains the list of unconfirmed transactions in memory.
        /// </summary>
        public string[] GetRawMempool()
        {
            return ((JArray)RpcSend("getrawmempool")).Select(p => p.AsString()).ToArray();
        }

        /// <summary>
        /// Obtains the list of unconfirmed transactions in memory.
        /// shouldGetUnverified = true
        /// </summary>
        public RpcRawMemPool GetRawMempoolBoth()
        {
            return RpcRawMemPool.FromJson(RpcSend("getrawmempool", true));
        }

        /// <summary>
        /// Returns the corresponding transaction information, based on the specified hash value.
        /// </summary>
        public string GetRawTransactionHex(string txid)
        {
            return RpcSend("getrawtransaction", txid).AsString();
        }

        /// <summary>
        /// Returns the corresponding transaction information, based on the specified hash value.
        /// verbose = true
        /// </summary>
        public RpcTransaction GetRawTransaction(string txid)
        {
            return RpcTransaction.FromJson(RpcSend("getrawtransaction", txid, true));
        }

        /// <summary>
        /// Returns the stored value, according to the contract script hash and the stored key.
        /// </summary>
        public string GetStorage(string script_hash, string key)
        {
            return RpcSend("getstorage", script_hash, key).AsString();
        }

        /// <summary>
        /// Returns the block index in which the transaction is found.
        /// </summary>
        public uint GetTransactionHeight(string txid)
        {
            return uint.Parse(RpcSend("gettransactionheight", txid).AsString());
        }

        /// <summary>
        /// Returns the current NEO consensus nodes information and voting status.
        /// </summary>
        public RpcValidator[] GetValidators()
        {
            return ((JArray)RpcSend("getvalidators")).Select(p => RpcValidator.FromJson(p)).ToArray();
        }

        /// <summary>
        /// Returns the version information about the queried node.
        /// </summary>
        public RpcVersion GetVersion()
        {
            return RpcVersion.FromJson(RpcSend("getversion"));
        }

        /// <summary>
        /// Returns the result after calling a smart contract at scripthash with the given operation and parameters.
        /// This RPC call does not affect the blockchain in any way.
        /// </summary>
        public RpcInvokeResult InvokeFunction(string address, string function, RpcStack[] stacks)
        {
            return RpcInvokeResult.FromJson(RpcSend("invokefunction", address, function, stacks.Select(p => p.ToJson()).ToArray()));
        }

        /// <summary>
        /// Returns the result after passing a script through the VM.
        /// This RPC call does not affect the blockchain in any way.
        /// </summary>
        public RpcInvokeResult InvokeScript(byte[] script, params UInt160[] scriptHashesForVerifying)
        {
            List<JObject> parameters = new List<JObject>
            {
                script.ToHexString()
            };
            parameters.AddRange(scriptHashesForVerifying.Select(p => (JObject)p.ToString()));
            return RpcInvokeResult.FromJson(RpcSend("invokescript", parameters.ToArray()));
        }

        /// <summary>
        /// Returns a list of plugins loaded by the node.
        /// </summary>
        public RpcPlugin[] ListPlugins()
        {
            return ((JArray)RpcSend("listplugins")).Select(p => RpcPlugin.FromJson(p)).ToArray();
        }

        /// <summary>
        /// Broadcasts a serialized transaction over the NEO network.
        /// </summary>
        public bool SendRawTransaction(byte[] rawTransaction)
        {
            return RpcSend("sendrawtransaction", rawTransaction.ToHexString()).AsBoolean();
        }

        /// <summary>
        /// Broadcasts a transaction over the NEO network.
        /// </summary>
        public bool SendRawTransaction(Transaction transaction)
        {
            return SendRawTransaction(transaction.ToArray());
        }

        /// <summary>
        /// Broadcasts a serialized block over the NEO network.
        /// </summary>
        public bool SubmitBlock(byte[] block)
        {
            return RpcSend("submitblock", block.ToHexString()).AsBoolean();
        }

        /// <summary>
        /// Verifies that the address is a correct NEO address.
        /// </summary>
        public RpcValidateAddressResult ValidateAddress(string address)
        {
            return RpcValidateAddressResult.FromJson(RpcSend("validateaddress", address));
        }
    }
}
