using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Neo.Network.RPC
{
    /// <summary>
    /// The RPC client to call NEO RPC methods
    /// </summary>
    public class RpcClient : IDisposable
    {
        private HttpClient httpClient;

        public RpcClient(string url, string rpcUser = default, string rpcPass = default)
        {
            httpClient = new HttpClient() { BaseAddress = new Uri(url) };
            if (!string.IsNullOrEmpty(rpcUser) && !string.IsNullOrEmpty(rpcPass))
            {
                string token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{rpcUser}:{rpcPass}"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
            }
        }

        public RpcClient(HttpClient client)
        {
            httpClient = client;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    httpClient?.Dispose();
                }

                httpClient = null;
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion

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
                JsonRpc = "2.0",
                Method = method,
                Params = paraArgs
            };
            return Send(request).Result;
        }

        #region Blockchain

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
        /// Queries contract information, according to the contract script hash.
        /// </summary>
        public ContractState GetContractState(string hash)
        {
            return RpcContractState.FromJson(RpcSend("getcontractstate", hash)).ContractState;
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
        public string GetRawTransactionHex(string txHash)
        {
            return RpcSend("getrawtransaction", txHash).AsString();
        }

        /// <summary>
        /// Returns the corresponding transaction information, based on the specified hash value.
        /// verbose = true
        /// </summary>
        public RpcTransaction GetRawTransaction(string txHash)
        {
            return RpcTransaction.FromJson(RpcSend("getrawtransaction", txHash, true));
        }

        /// <summary>
        /// Returns the stored value, according to the contract script hash (or Id) and the stored key.
        /// </summary>
        public string GetStorage(string scriptHashOrId, string key)
        {
            if (int.TryParse(scriptHashOrId, out int id))
            {
                return RpcSend("getstorage", id, key).AsString();
            }

            return RpcSend("getstorage", scriptHashOrId, key).AsString();
        }

        /// <summary>
        /// Returns the block index in which the transaction is found.
        /// </summary>
        public uint GetTransactionHeight(string txHash)
        {
            return uint.Parse(RpcSend("gettransactionheight", txHash).AsString());
        }

        /// <summary>
        /// Returns the current NEO consensus nodes information and voting status.
        /// </summary>
        public RpcValidator[] GetValidators()
        {
            return ((JArray)RpcSend("getvalidators")).Select(p => RpcValidator.FromJson(p)).ToArray();
        }

        #endregion Blockchain

        #region Node

        /// <summary>
        /// Gets the current number of connections for the node.
        /// </summary>
        public int GetConnectionCount()
        {
            return (int)RpcSend("getconnectioncount").AsNumber();
        }

        /// <summary>
        /// Gets the list of nodes that the node is currently connected/disconnected from.
        /// </summary>
        public RpcPeers GetPeers()
        {
            return RpcPeers.FromJson(RpcSend("getpeers"));
        }

        /// <summary>
        /// Returns the version information about the queried node.
        /// </summary>
        public RpcVersion GetVersion()
        {
            return RpcVersion.FromJson(RpcSend("getversion"));
        }

        /// <summary>
        /// Broadcasts a serialized transaction over the NEO network.
        /// </summary>
        public UInt256 SendRawTransaction(byte[] rawTransaction)
        {
            return UInt256.Parse(RpcSend("sendrawtransaction", rawTransaction.ToHexString())["hash"].AsString());
        }

        /// <summary>
        /// Broadcasts a transaction over the NEO network.
        /// </summary>
        public UInt256 SendRawTransaction(Transaction transaction)
        {
            return SendRawTransaction(transaction.ToArray());
        }

        /// <summary>
        /// Broadcasts a serialized block over the NEO network.
        /// </summary>
        public UInt256 SubmitBlock(byte[] block)
        {
            return UInt256.Parse(RpcSend("submitblock", block.ToHexString())["hash"].AsString());
        }

        #endregion Node

        #region SmartContract

        /// <summary>
        /// Returns the result after calling a smart contract at scripthash with the given operation and parameters.
        /// This RPC call does not affect the blockchain in any way.
        /// </summary>
        public RpcInvokeResult InvokeFunction(string scriptHash, string operation, RpcStack[] stacks)
        {
            return RpcInvokeResult.FromJson(RpcSend("invokefunction", scriptHash, operation, stacks.Select(p => p.ToJson()).ToArray()));
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

        #endregion SmartContract

        #region Utilities

        /// <summary>
        /// Returns a list of plugins loaded by the node.
        /// </summary>
        public RpcPlugin[] ListPlugins()
        {
            return ((JArray)RpcSend("listplugins")).Select(p => RpcPlugin.FromJson(p)).ToArray();
        }

        /// <summary>
        /// Verifies that the address is a correct NEO address.
        /// </summary>
        public RpcValidateAddressResult ValidateAddress(string address)
        {
            return RpcValidateAddressResult.FromJson(RpcSend("validateaddress", address));
        }

        #endregion Utilities

        #region Wallet

        /// <summary>
        /// Close the wallet opened by RPC.
        /// </summary>
        public bool CloseWallet()
        {
            return RpcSend("closewallet").AsBoolean();
        }

        /// <summary>
        /// Exports the private key of the specified address.
        /// </summary>
        public string DumpPrivKey(string address)
        {
            return RpcSend("dumpprivkey", address).AsString();
        }

        /// <summary>
        /// Returns the balance of the corresponding asset in the wallet, based on the specified asset Id.
        /// This method applies to assets that conform to NEP-5 standards.
        /// </summary>
        /// <returns>new address as string</returns>
        public BigDecimal GetBalance(string assetId)
        {
            byte decimals = new Nep5API(this).Decimals(UInt160.Parse(assetId));
            BigInteger balance = BigInteger.Parse(RpcSend("getbalance", assetId)["balance"].AsString());
            return new BigDecimal(balance, decimals);
        }

        /// <summary>
        /// Creates a new account in the wallet opened by RPC.
        /// </summary>
        public string GetNewAddress()
        {
            return RpcSend("getnewaddress").AsString();
        }

        /// <summary>
        /// Gets the amount of unclaimed GAS in the wallet.
        /// </summary>
        public BigInteger GetUnclaimedGas()
        {
            return BigInteger.Parse(RpcSend("getunclaimedgas").AsString());
        }

        /// <summary>
        /// Imports the private key to the wallet.
        /// </summary>
        public RpcAccount ImportPrivKey(string wif)
        {
            return RpcAccount.FromJson(RpcSend("importprivkey", wif));
        }

        /// <summary>
        /// Lists all the accounts in the current wallet.
        /// </summary>
        public List<RpcAccount> ListAddress()
        {
            return ((JArray)RpcSend("listaddress")).Select(p => RpcAccount.FromJson(p)).ToList();
        }

        /// <summary>
        /// Open wallet file in the provider's machine.
        /// By default, this method is disabled by RpcServer config.json.
        /// </summary>
        public bool OpenWallet(string path, string password)
        {
            return RpcSend("openwallet", path, password).AsBoolean();
        }

        /// <summary>
        /// Transfer from the specified address to the destination address.
        /// </summary>
        /// <returns>This function returns Signed Transaction JSON if successful, ContractParametersContext JSON if signing failed.</returns>
        public JObject SendFrom(string assetId, string fromAddress, string toAddress, string amount)
        {
            return RpcSend("sendfrom", assetId, fromAddress, toAddress, amount);
        }

        /// <summary>
        /// Bulk transfer order, and you can specify a sender address.
        /// </summary>
        /// <returns>This function returns Signed Transaction JSON if successful, ContractParametersContext JSON if signing failed.</returns>
        public JObject SendMany(string fromAddress, IEnumerable<RpcTransferOut> outputs)
        {
            var parameters = new List<JObject>();
            if (!string.IsNullOrEmpty(fromAddress))
            {
                parameters.Add(fromAddress);
            }
            parameters.Add(outputs.Select(p => p.ToJson()).ToArray());

            return RpcSend("sendmany", paraArgs: parameters.ToArray());
        }

        /// <summary>
        /// Transfer asset from the wallet to the destination address.
        /// </summary>
        /// <returns>This function returns Signed Transaction JSON if successful, ContractParametersContext JSON if signing failed.</returns>
        public JObject SendToAddress(string assetId, string address, string amount)
        {
            return RpcSend("sendtoaddress", assetId, address, amount);
        }

        #endregion Utilities

        #region Plugins

        /// <summary>
        /// Returns the contract log based on the specified txHash. The complete contract logs are stored under the ApplicationLogs directory.
        /// This method is provided by the plugin ApplicationLogs.
        /// </summary>
        public RpcApplicationLog GetApplicationLog(string txHash)
        {
            return RpcApplicationLog.FromJson(RpcSend("getapplicationlog", txHash));
        }

        /// <summary>
        /// Returns all the NEP-5 transaction information occurred in the specified address.
        /// This method is provided by the plugin RpcNep5Tracker.
        /// </summary>
        /// <param name="address">The address to query the transaction information.</param>
        /// <param name="startTimestamp">The start block Timestamp, default to seven days before UtcNow</param>
        /// <param name="endTimestamp">The end block Timestamp, default to UtcNow</param>
        public RpcNep5Transfers GetNep5Transfers(string address, ulong? startTimestamp = default, ulong? endTimestamp = default)
        {
            startTimestamp ??= 0;
            endTimestamp ??= DateTime.UtcNow.ToTimestampMS();
            return RpcNep5Transfers.FromJson(RpcSend("getnep5transfers", address, startTimestamp, endTimestamp));
        }

        /// <summary>
        /// Returns the balance of all NEP-5 assets in the specified address.
        /// This method is provided by the plugin RpcNep5Tracker.
        /// </summary>
        public RpcNep5Balances GetNep5Balances(string address)
        {
            return RpcNep5Balances.FromJson(RpcSend("getnep5balances", address));
        }

        #endregion Plugins
    }
}
