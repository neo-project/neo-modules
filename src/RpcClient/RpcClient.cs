using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC.Models;
using Neo.SmartContract.Manifest;
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
        private readonly HttpClient httpClient;
        private readonly string baseAddress;

        public RpcClient(string url, string rpcUser = default, string rpcPass = default)
        {
            httpClient = new HttpClient();
            baseAddress = url;
            if (!string.IsNullOrEmpty(rpcUser) && !string.IsNullOrEmpty(rpcPass))
            {
                string token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{rpcUser}:{rpcPass}"));
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
            }
        }

        public RpcClient(HttpClient client, string url)
        {
            httpClient = client;
            baseAddress = url;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    httpClient.Dispose();
                }

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
            if (disposedValue) throw new ObjectDisposedException(nameof(RpcClient));

            var requestJson = request.ToJson().ToString();
            using var result = await httpClient.PostAsync(baseAddress, new StringContent(requestJson, Encoding.UTF8)).ConfigureAwait(false);
            var content = await result.Content.ReadAsStringAsync();
            var response = RpcResponse.FromJson(JObject.Parse(content));
            response.RawResponse = content;

            if (response.Error != null)
            {
                throw new RpcException(response.Error.Code, response.Error.Message);
            }

            return response;
        }

        public virtual async Task<JObject> RpcSendAsync(string method, params JObject[] paraArgs)
        {
            var request = new RpcRequest
            {
                Id = 1,
                JsonRpc = "2.0",
                Method = method,
                Params = paraArgs
            };

            var response = await SendAsync(request).ConfigureAwait(false);
            return response.Result;
        }

        #region Blockchain

        /// <summary>
        /// Returns the hash of the tallest block in the main chain.
        /// </summary>
        public async Task<string> GetBestBlockHash()
        {
            var result = await RpcSendAsync("getbestblockhash").ConfigureAwait(false);
            return result.AsString();
        }

        /// <summary>
        /// Returns the hash of the tallest block in the main chain.
        /// The serialized information of the block is returned, represented by a hexadecimal string.
        /// </summary>
        public async Task<string> GetBlockHex(string hashOrIndex)
        {
            var result = int.TryParse(hashOrIndex, out int index)
                ? await RpcSendAsync("getblock", index).ConfigureAwait(false)
                : await RpcSendAsync("getblock", hashOrIndex).ConfigureAwait(false);
            return result.AsString();
        }

        /// <summary>
        /// Returns the hash of the tallest block in the main chain.
        /// </summary>
        public async Task<RpcBlock> GetBlock(string hashOrIndex)
        {
            var result = int.TryParse(hashOrIndex, out int index)
                ? await RpcSendAsync("getblock", index, true).ConfigureAwait(false)
                : await RpcSendAsync("getblock", hashOrIndex, true).ConfigureAwait(false);

            return RpcBlock.FromJson(result);
        }

        /// <summary>
        /// Gets the number of blocks in the main chain.
        /// </summary>
        public async Task<uint> GetBlockCount()
        {
            var result = await RpcSendAsync("getblockcount").ConfigureAwait(false);
            return (uint)result.AsNumber();
        }

        /// <summary>
        /// Returns the hash value of the corresponding block, based on the specified index.
        /// </summary>
        public async Task<string> GetBlockHash(int index)
        {
            var result = await RpcSendAsync("getblockhash").ConfigureAwait(false);
            return result.AsString();
        }

        /// <summary>
        /// Returns the corresponding block header information according to the specified script hash.
        /// </summary>
        public async Task<string> GetBlockHeaderHex(string hashOrIndex)
        {
            var result = int.TryParse(hashOrIndex, out int index)
                ? await RpcSendAsync("getblockheader", index).ConfigureAwait(false)
                : await RpcSendAsync("getblockheader", hashOrIndex).ConfigureAwait(false);
            return result.AsString();
        }

        /// <summary>
        /// Returns the corresponding block header information according to the specified script hash.
        /// </summary>
        public async Task<RpcBlockHeader> GetBlockHeader(string hashOrIndex)
        {
            var result = int.TryParse(hashOrIndex, out int index)
                ? await RpcSendAsync("getblockheader", index, true).ConfigureAwait(false)
                : await RpcSendAsync("getblockheader", hashOrIndex, true).ConfigureAwait(false);

            return RpcBlockHeader.FromJson(result);
        }

        /// <summary>
        /// Queries contract information, according to the contract script hash.
        /// </summary>
        public async Task<ContractState> GetContractState(string hash)
        {
            var result = await RpcSendAsync("getcontractstate", hash).ConfigureAwait(false);
            return ContractStateFromJson(result);
        }

        public static ContractState ContractStateFromJson(JObject json)
        {
            return new ContractState
            {
                Id = (int)json["id"].AsNumber(),
                Script = Convert.FromBase64String(json["script"].AsString()),
                Manifest = ContractManifest.FromJson(json["manifest"])
            };
        }

        /// <summary>
        /// Obtains the list of unconfirmed transactions in memory.
        /// </summary>
        public async Task<string[]> GetRawMempool()
        {
            var result = await RpcSendAsync("getrawmempool").ConfigureAwait(false);
            return ((JArray)result).Select(p => p.AsString()).ToArray();
        }

        /// <summary>
        /// Obtains the list of unconfirmed transactions in memory.
        /// shouldGetUnverified = true
        /// </summary>
        public async Task<RpcRawMemPool> GetRawMempoolBoth()
        {
            var result = await RpcSendAsync("getrawmempool", true).ConfigureAwait(false);
            return RpcRawMemPool.FromJson(result);
        }

        /// <summary>
        /// Returns the corresponding transaction information, based on the specified hash value.
        /// </summary>
        public async Task<string> GetRawTransactionHex(string txHash)
        {
            var result = await RpcSendAsync("getrawtransaction", txHash).ConfigureAwait(false);
            return result.AsString();
        }

        /// <summary>
        /// Returns the corresponding transaction information, based on the specified hash value.
        /// verbose = true
        /// </summary>
        public async Task<RpcTransaction> GetRawTransaction(string txHash)
        {
            var result = await RpcSendAsync("getrawtransaction", txHash, true).ConfigureAwait(false);
            return RpcTransaction.FromJson(result);
        }

        /// <summary>
        /// Returns the stored value, according to the contract script hash (or Id) and the stored key.
        /// </summary>
        public async Task<string> GetStorage(string scriptHashOrId, string key)
        {
            var result = int.TryParse(scriptHashOrId, out int id)
                ? await RpcSendAsync("getstorage", id, key).ConfigureAwait(false)
                : await RpcSendAsync("getstorage", scriptHashOrId, key).ConfigureAwait(false);
            return result.AsString();
        }

        /// <summary>
        /// Returns the block index in which the transaction is found.
        /// </summary>
        public async Task<uint> GetTransactionHeight(string txHash)
        {
            var result = await RpcSendAsync("gettransactionheight", txHash).ConfigureAwait(false);
            return uint.Parse(result.AsString());
        }

        /// <summary>
        /// Returns the current NEO consensus nodes information and voting status.
        /// </summary>
        public async Task<RpcValidator[]> GetValidators()
        {
            var result = await RpcSendAsync("getvalidators").ConfigureAwait(false);
            return ((JArray)result).Select(p => RpcValidator.FromJson(p)).ToArray();
        }

        #endregion Blockchain

        #region Node

        /// <summary>
        /// Gets the current number of connections for the node.
        /// </summary>
        public async Task<int> GetConnectionCount()
        {
            var result = await RpcSendAsync("getconnectioncount").ConfigureAwait(false);
            return (int)result.AsNumber();
        }

        /// <summary>
        /// Gets the list of nodes that the node is currently connected/disconnected from.
        /// </summary>
        public async Task<RpcPeers> GetPeers()
        {
            var result = await RpcSendAsync("getpeers").ConfigureAwait(false);
            return RpcPeers.FromJson(result);
        }

        /// <summary>
        /// Returns the version information about the queried node.
        /// </summary>
        public async Task<RpcVersion> GetVersion()
        {
            var result = await RpcSendAsync("getversion").ConfigureAwait(false);
            return RpcVersion.FromJson(result);
        }

        /// <summary>
        /// Broadcasts a serialized transaction over the NEO network.
        /// </summary>
        public async Task<UInt256> SendRawTransaction(byte[] rawTransaction)
        {
            var result = await RpcSendAsync("sendrawtransaction", rawTransaction.ToHexString()).ConfigureAwait(false);
            return UInt256.Parse(result["hash"].AsString());
        }

        /// <summary>
        /// Broadcasts a transaction over the NEO network.
        /// </summary>
        public Task<UInt256> SendRawTransaction(Transaction transaction)
        {
            return SendRawTransaction(transaction.ToArray());
        }

        /// <summary>
        /// Broadcasts a serialized block over the NEO network.
        /// </summary>
        public async Task<UInt256> SubmitBlock(byte[] block)
        {
            var result = await RpcSendAsync("submitblock", block.ToHexString()).ConfigureAwait(false);
            return UInt256.Parse(result["hash"].AsString());
        }

        #endregion Node

        #region SmartContract

        /// <summary>
        /// Returns the result after calling a smart contract at scripthash with the given operation and parameters.
        /// This RPC call does not affect the blockchain in any way.
        /// </summary>
        public async Task<RpcInvokeResult> InvokeFunction(string scriptHash, string operation, RpcStack[] stacks, params Signer[] signer)
        {
            List<JObject> parameters = new List<JObject> { scriptHash, operation, stacks.Select(p => p.ToJson()).ToArray() };
            if (signer.Length > 0)
            {
                parameters.Add(signer.Select(p => (JObject)p.ToJson()).ToArray());
            }
            var result = await RpcSendAsync("invokefunction", parameters.ToArray()).ConfigureAwait(false);
            return RpcInvokeResult.FromJson(result);
        }

        /// <summary>
        /// Returns the result after passing a script through the VM.
        /// This RPC call does not affect the blockchain in any way.
        /// </summary>
        public async Task<RpcInvokeResult> InvokeScript(byte[] script, params Signer[] signers)
        {
            List<JObject> parameters = new List<JObject> { script.ToHexString() };
            if (signers.Length > 0)
            {
                parameters.Add(signers.Select(p => p.ToJson()).ToArray());
            }
            var result = await RpcSendAsync("invokescript", parameters.ToArray()).ConfigureAwait(false);
            return RpcInvokeResult.FromJson(result);
        }

        public async Task<RpcUnclaimedGas> GetUnclaimedGas(string address)
        {
            var result = await RpcSendAsync("getunclaimedgas", address).ConfigureAwait(false);
            return RpcUnclaimedGas.FromJson(result);
        }

        #endregion SmartContract

        #region Utilities

        /// <summary>
        /// Returns a list of plugins loaded by the node.
        /// </summary>
        public async Task<RpcPlugin[]> ListPlugins()
        {
            var result = await RpcSendAsync("listplugins").ConfigureAwait(false);
            return ((JArray)result).Select(p => RpcPlugin.FromJson(p)).ToArray();
        }

        /// <summary>
        /// Verifies that the address is a correct NEO address.
        /// </summary>
        public async Task<RpcValidateAddressResult> ValidateAddress(string address)
        {
            var result = await RpcSendAsync("validateaddress", address).ConfigureAwait(false);
            return RpcValidateAddressResult.FromJson(result);
        }

        #endregion Utilities

        #region Wallet

        /// <summary>
        /// Close the wallet opened by RPC.
        /// </summary>
        public async Task<bool> CloseWallet()
        {
            var result = await RpcSendAsync("closewallet").ConfigureAwait(false);
            return result.AsBoolean();
        }

        /// <summary>
        /// Exports the private key of the specified address.
        /// </summary>
        public async Task<string> DumpPrivKey(string address)
        {
            var result = await RpcSendAsync("dumpprivkey", address).ConfigureAwait(false);
            return result.AsString();
        }

        /// <summary>
        /// Creates a new account in the wallet opened by RPC.
        /// </summary>
        public async Task<string> GetNewAddress()
        {
            var result = await RpcSendAsync("getnewaddress").ConfigureAwait(false);
            return result.AsString();
        }

        /// <summary>
        /// Returns the balance of the corresponding asset in the wallet, based on the specified asset Id.
        /// This method applies to assets that conform to NEP-5 standards.
        /// </summary>
        /// <returns>new address as string</returns>
        public async Task<BigDecimal> GetWalletBalance(string assetId)
        {
            byte decimals = await (new Nep5API(this).Decimals(UInt160.Parse(assetId)));
            var result = await RpcSendAsync("getwalletbalance", assetId).ConfigureAwait(false);
            BigInteger balance = BigInteger.Parse(result["balance"].AsString());
            return new BigDecimal(balance, decimals);
        }

        /// <summary>
        /// Gets the amount of unclaimed GAS in the wallet.
        /// </summary>
        public async Task<BigInteger> GetWalletUnclaimedGas()
        {
            var result = await RpcSendAsync("getwalletunclaimedgas").ConfigureAwait(false);
            return BigInteger.Parse(result.AsString());
        }

        /// <summary>
        /// Imports the private key to the wallet.
        /// </summary>
        public async Task<RpcAccount> ImportPrivKey(string wif)
        {
            var result = await RpcSendAsync("importprivkey", wif).ConfigureAwait(false);
            return RpcAccount.FromJson(result);
        }

        /// <summary>
        /// Lists all the accounts in the current wallet.
        /// </summary>
        public async Task<List<RpcAccount>> ListAddress()
        {
            var result = await RpcSendAsync("listaddress").ConfigureAwait(false);
            return ((JArray)result).Select(p => RpcAccount.FromJson(p)).ToList();
        }

        /// <summary>
        /// Open wallet file in the provider's machine.
        /// By default, this method is disabled by RpcServer config.json.
        /// </summary>
        public async Task<bool> OpenWallet(string path, string password)
        {
            var result = await RpcSendAsync("openwallet", path, password).ConfigureAwait(false);
            return result.AsBoolean();
        }

        /// <summary>
        /// Transfer from the specified address to the destination address.
        /// </summary>
        /// <returns>This function returns Signed Transaction JSON if successful, ContractParametersContext JSON if signing failed.</returns>
        public async Task<JObject> SendFrom(string assetId, string fromAddress, string toAddress, string amount)
        {
            return await RpcSendAsync("sendfrom", assetId, fromAddress, toAddress, amount).ConfigureAwait(false);
        }

        /// <summary>
        /// Bulk transfer order, and you can specify a sender address.
        /// </summary>
        /// <returns>This function returns Signed Transaction JSON if successful, ContractParametersContext JSON if signing failed.</returns>
        public async Task<JObject> SendMany(string fromAddress, IEnumerable<RpcTransferOut> outputs)
        {
            var parameters = new List<JObject>();
            if (!string.IsNullOrEmpty(fromAddress))
            {
                parameters.Add(fromAddress);
            }
            parameters.Add(outputs.Select(p => p.ToJson()).ToArray());

            return await RpcSendAsync("sendmany", paraArgs: parameters.ToArray()).ConfigureAwait(false);
        }

        /// <summary>
        /// Transfer asset from the wallet to the destination address.
        /// </summary>
        /// <returns>This function returns Signed Transaction JSON if successful, ContractParametersContext JSON if signing failed.</returns>
        public async Task<JObject> SendToAddress(string assetId, string address, string amount)
        {
            return await RpcSendAsync("sendtoaddress", assetId, address, amount).ConfigureAwait(false);
        }

        #endregion Wallet

        #region Plugins

        /// <summary>
        /// Returns the contract log based on the specified txHash. The complete contract logs are stored under the ApplicationLogs directory.
        /// This method is provided by the plugin ApplicationLogs.
        /// </summary>
        public async Task<RpcApplicationLog> GetApplicationLog(string txHash)
        {
            var result = await RpcSendAsync("getapplicationlog", txHash).ConfigureAwait(false);
            return RpcApplicationLog.FromJson(result);
        }

        /// <summary>
        /// Returns all the NEP-5 transaction information occurred in the specified address.
        /// This method is provided by the plugin RpcNep5Tracker.
        /// </summary>
        /// <param name="address">The address to query the transaction information.</param>
        /// <param name="startTimestamp">The start block Timestamp, default to seven days before UtcNow</param>
        /// <param name="endTimestamp">The end block Timestamp, default to UtcNow</param>
        public async Task<RpcNep5Transfers> GetNep5Transfers(string address, ulong? startTimestamp = default, ulong? endTimestamp = default)
        {
            startTimestamp ??= 0;
            endTimestamp ??= DateTime.UtcNow.ToTimestampMS();
            var result = await RpcSendAsync("getnep5transfers", address, startTimestamp, endTimestamp).ConfigureAwait(false);
            return RpcNep5Transfers.FromJson(result);
        }

        /// <summary>
        /// Returns the balance of all NEP-5 assets in the specified address.
        /// This method is provided by the plugin RpcNep5Tracker.
        /// </summary>
        public async Task<RpcNep5Balances> GetNep5Balances(string address)
        {
            var result = await RpcSendAsync("getnep5balances", address).ConfigureAwait(false);
            return RpcNep5Balances.FromJson(result);
        }

        #endregion Plugins
    }
}
