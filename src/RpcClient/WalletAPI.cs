using Neo.Cryptography.ECC;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC.Models;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Neo.Network.RPC
{
    /// <summary>
    /// Wallet Common APIs
    /// </summary>
    public class WalletAPI
    {
        private readonly RpcClient rpcClient;
        private readonly Nep5API nep5API;

        /// <summary>
        /// WalletAPI Constructor
        /// </summary>
        /// <param name="rpc">the RPC client to call NEO RPC methods</param>
        public WalletAPI(RpcClient rpc)
        {
            rpcClient = rpc;
            nep5API = new Nep5API(rpc);
        }

        /// <summary>
        /// Get unclaimed gas with address, scripthash or public key string
        /// </summary>
        /// <param name="account">address, scripthash or public key string
        /// Example: address ("Ncm9TEzrp8SSer6Wa3UCSLTRnqzwVhCfuE"), scripthash ("0xb0a31817c80ad5f87b6ed390ecb3f9d312f7ceb8"), public key ("02f9ec1fd0a98796cf75b586772a4ddd41a0af07a1dbdf86a7238f74fb72503575")</param>
        /// <returns></returns>
        public Task<decimal> GetUnclaimedGasAsync(string account)
        {
            UInt160 accountHash = Utility.GetScriptHash(account);
            return GetUnclaimedGasAsync(accountHash);
        }

        /// <summary>
        /// Get unclaimed gas
        /// </summary>
        /// <param name="account">account scripthash</param>
        /// <returns></returns>
        public async Task<decimal> GetUnclaimedGasAsync(UInt160 account)
        {
            UInt160 scriptHash = NativeContract.NEO.Hash;
            var blockCount = await rpcClient.GetBlockCountAsync().ConfigureAwait(false);
            var result = await nep5API.TestInvokeAsync(scriptHash, "unclaimedGas", account, blockCount - 1).ConfigureAwait(false);
            BigInteger balance = result.Stack.Single().GetInteger();
            return ((decimal)balance) / (long)NativeContract.GAS.Factor;
        }

        /// <summary>
        /// Get Neo Balance
        /// </summary>
        /// <param name="account">address, scripthash or public key string
        /// Example: address ("Ncm9TEzrp8SSer6Wa3UCSLTRnqzwVhCfuE"), scripthash ("0xb0a31817c80ad5f87b6ed390ecb3f9d312f7ceb8"), public key ("02f9ec1fd0a98796cf75b586772a4ddd41a0af07a1dbdf86a7238f74fb72503575")</param>
        /// <returns></returns>
        public async Task<uint> GetNeoBalanceAsync(string account)
        {
            BigInteger balance = await GetTokenBalanceAsync(NativeContract.NEO.Hash.ToString(), account).ConfigureAwait(false);
            return (uint)balance;
        }

        /// <summary>
        /// Get Gas Balance
        /// </summary>
        /// <param name="account">address, scripthash or public key string
        /// Example: address ("Ncm9TEzrp8SSer6Wa3UCSLTRnqzwVhCfuE"), scripthash ("0xb0a31817c80ad5f87b6ed390ecb3f9d312f7ceb8"), public key ("02f9ec1fd0a98796cf75b586772a4ddd41a0af07a1dbdf86a7238f74fb72503575")</param>
        /// <returns></returns>
        public async Task<decimal> GetGasBalanceAsync(string account)
        {
            BigInteger balance = await GetTokenBalanceAsync(NativeContract.GAS.Hash.ToString(), account).ConfigureAwait(false);
            return ((decimal)balance) / (long)NativeContract.GAS.Factor;
        }

        /// <summary>
        /// Get token balance with string parameters
        /// </summary>
        /// <param name="tokenHash">token script hash, Example: "0x43cf98eddbe047e198a3e5d57006311442a0ca15"(NEO)</param>
        /// <param name="account">address, scripthash or public key string
        /// Example: address ("Ncm9TEzrp8SSer6Wa3UCSLTRnqzwVhCfuE"), scripthash ("0xb0a31817c80ad5f87b6ed390ecb3f9d312f7ceb8"), public key ("02f9ec1fd0a98796cf75b586772a4ddd41a0af07a1dbdf86a7238f74fb72503575")</param>
        /// <returns></returns>
        public Task<BigInteger> GetTokenBalanceAsync(string tokenHash, string account)
        {
            UInt160 scriptHash = Utility.GetScriptHash(tokenHash);
            UInt160 accountHash = Utility.GetScriptHash(account);
            return nep5API.BalanceOfAsync(scriptHash, accountHash);
        }

        /// <summary>
        /// The GAS is claimed when doing NEO transfer
        /// This function will transfer NEO balance from account to itself
        /// </summary>
        /// <param name="key">wif or private key
        /// Example: WIF ("KyXwTh1hB76RRMquSvnxZrJzQx7h9nQP2PCRL38v6VDb5ip3nf1p"), PrivateKey ("450d6c2a04b5b470339a745427bae6828400cf048400837d73c415063835e005")</param>
        /// <returns>The transaction sended</returns>
        public Task<Transaction> ClaimGasAsync(string key)
        {
            KeyPair keyPair = Utility.GetKeyPair(key);
            return ClaimGasAsync(keyPair);
        }

        /// <summary>
        /// The GAS is claimed when doing NEO transfer
        /// This function will transfer NEO balance from account to itself
        /// </summary>
        /// <param name="keyPair">keyPair</param>
        /// <returns>The transaction sended</returns>
        public async Task<Transaction> ClaimGasAsync(KeyPair keyPair)
        {
            UInt160 toHash = Contract.CreateSignatureRedeemScript(keyPair.PublicKey).ToScriptHash();
            BigInteger balance = await nep5API.BalanceOfAsync(NativeContract.NEO.Hash, toHash).ConfigureAwait(false);
            Transaction transaction = await nep5API.CreateTransferTxAsync(NativeContract.NEO.Hash, keyPair, toHash, balance).ConfigureAwait(false);
            await rpcClient.SendRawTransactionAsync(transaction).ConfigureAwait(false);
            return transaction;
        }

        /// <summary>
        /// Transfer NEP5 token balance, with common data types
        /// </summary>
        /// <param name="tokenHash">nep5 token script hash, Example: scripthash ("0xb0a31817c80ad5f87b6ed390ecb3f9d312f7ceb8")</param>
        /// <param name="fromKey">wif or private key
        /// Example: WIF ("KyXwTh1hB76RRMquSvnxZrJzQx7h9nQP2PCRL38v6VDb5ip3nf1p"), PrivateKey ("450d6c2a04b5b470339a745427bae6828400cf048400837d73c415063835e005")</param>
        /// <param name="toAddress">address or account script hash</param>
        /// <param name="amount">token amount</param>
        /// <returns></returns>
        public async Task<Transaction> TransferAsync(string tokenHash, string fromKey, string toAddress, decimal amount)
        {
            UInt160 scriptHash = Utility.GetScriptHash(tokenHash);
            var decimals = await nep5API.DecimalsAsync(scriptHash).ConfigureAwait(false);

            KeyPair from = Utility.GetKeyPair(fromKey);
            UInt160 to = Utility.GetScriptHash(toAddress);
            BigInteger amountInteger = amount.ToBigInteger(decimals);
            return await TransferAsync(scriptHash, from, to, amountInteger).ConfigureAwait(false);
        }

        /// <summary>
        /// Transfer NEP5 token from single-sig account
        /// </summary>
        /// <param name="scriptHash">contract script hash</param>
        /// <param name="from">from KeyPair</param>
        /// <param name="to">to account script hash</param>
        /// <param name="amountInteger">transfer amount</param>
        /// <returns></returns>
        public async Task<Transaction> TransferAsync(UInt160 scriptHash, KeyPair from, UInt160 to, BigInteger amountInteger)
        {
            Transaction transaction = await nep5API.CreateTransferTxAsync(scriptHash, from, to, amountInteger).ConfigureAwait(false);
            await rpcClient.SendRawTransactionAsync(transaction).ConfigureAwait(false);
            return transaction;
        }

        /// <summary>
        /// Transfer NEP5 token from multi-sig account
        /// </summary>
        /// <param name="scriptHash">contract script hash</param>
        /// <param name="m">multi-sig min signature count</param>
        /// <param name="pubKeys">multi-sig pubKeys</param>
        /// <param name="keys">sign keys</param>
        /// <param name="to">to account</param>
        /// <param name="amountInteger">transfer amount</param>
        /// <returns></returns>
        public async Task<Transaction> TransferAsync(UInt160 scriptHash, int m, ECPoint[] pubKeys, KeyPair[] keys, UInt160 to, BigInteger amountInteger)
        {
            Transaction transaction = await nep5API.CreateTransferTxAsync(scriptHash, m, pubKeys, keys, to, amountInteger).ConfigureAwait(false);
            await rpcClient.SendRawTransactionAsync(transaction).ConfigureAwait(false);
            return transaction;
        }

        /// <summary>
        /// Wait until the transaction is observable block chain
        /// </summary>
        /// <param name="transaction">the transaction to observe</param>
        /// <param name="timeout">TimeoutException throws after "timeout" seconds</param>
        /// <returns>the Transaction state, including vmState and blockhash</returns>
        public async Task<RpcTransaction> WaitTransactionAsync(Transaction transaction, int timeout = 60)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(timeout);
            RpcTransaction rpcTx = null;
            while (rpcTx == null || rpcTx.Confirmations == null)
            {
                if (deadline < DateTime.UtcNow)
                {
                    throw new TimeoutException();
                }

                try
                {
                    rpcTx = await rpcClient.GetRawTransactionAsync(transaction.Hash.ToString()).ConfigureAwait(false);
                    if (rpcTx == null || rpcTx.Confirmations == null)
                    {
                        await Task.Delay((int)Blockchain.MillisecondsPerBlock / 2);
                    }
                }
                catch (Exception) { }
            }
            return rpcTx;
        }
    }
}
