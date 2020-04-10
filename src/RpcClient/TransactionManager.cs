using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC.Models;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Network.RPC
{
    /// <summary>
    /// This class helps to create transaction with RPC API.
    /// </summary>
    public class TransactionManager
    {
        private readonly RpcClient rpcClient;
        private readonly PolicyAPI policyAPI;
        private readonly Nep5API nep5API;
        private readonly UInt160 sender;

        private class SignItem { public Contract Contract; public HashSet<KeyPair> KeyPairs; }

        /// <summary>
        /// The Transaction context to manage the witnesses
        /// </summary>
        private ContractParametersContext context;

        /// <summary>
        /// This container stores the keys for sign the transaction
        /// </summary>
        private List<SignItem> signStore;

        /// <summary>
        /// The Transaction managed by this class
        /// </summary>
        public Transaction Tx { get; private set; }

        /// <summary>
        /// TransactionManager Constructor
        /// </summary>
        /// <param name="rpc">the RPC client to call NEO RPC API</param>
        /// <param name="sender">the account script hash of sender</param>
        public TransactionManager(RpcClient rpc, UInt160 sender)
        {
            rpcClient = rpc;
            policyAPI = new PolicyAPI(rpc);
            nep5API = new Nep5API(rpc);
            this.sender = sender;
        }

        /// <summary>
        /// Create an unsigned Transaction object with given parameters.
        /// </summary>
        /// <param name="script">Transaction Script</param>
        /// <param name="attributes">Transaction Attributes</param>
        /// <param name="cosigners">Transaction Cosigners</param>
        /// <returns></returns>
        public TransactionManager MakeTransaction(byte[] script, TransactionAttribute[] attributes = null, Cosigner[] cosigners = null)
        {
            var random = new Random();
            uint height = rpcClient.GetBlockCount() - 1;
            Tx = new Transaction
            {
                Version = 0,
                Nonce = (uint)random.Next(),
                Script = script,
                Sender = sender,
                ValidUntilBlock = height + Transaction.MaxValidUntilBlockIncrement,
                Attributes = attributes ?? Array.Empty<TransactionAttribute>(),
                Cosigners = cosigners ?? Array.Empty<Cosigner>(),
                Witnesses = Array.Empty<Witness>()
            };

            // Add witness hashes parameter to pass CheckWitness
            UInt160[] hashes = Tx.GetScriptHashesForVerifying(null);
            RpcInvokeResult result = rpcClient.InvokeScript(script, hashes);
            Tx.SystemFee = Math.Max(long.Parse(result.GasConsumed) - ApplicationEngine.GasFree, 0);

            context = new ContractParametersContext(Tx);
            signStore = new List<SignItem>();

            return this;
        }

        /// <summary>
        /// Calculate NetworkFee
        /// </summary>
        /// <returns></returns>
        private long CalculateNetworkFee()
        {
            long networkFee = 0;
            UInt160[] hashes = Tx.GetScriptHashesForVerifying(null);
            int size = Transaction.HeaderSize + Tx.Attributes.GetVarSize() + Tx.Cosigners.GetVarSize() + Tx.Script.GetVarSize() + IO.Helper.GetVarSize(hashes.Length);
            foreach (UInt160 hash in hashes)
            {
                byte[] witness_script = null;

                // calculate NetworkFee
                witness_script = signStore.FirstOrDefault(p => p.Contract.ScriptHash == hash)?.Contract?.Script;
                if (witness_script is null || witness_script.Length == 0)
                {
                    try
                    {
                        witness_script = rpcClient.GetContractState(hash.ToString())?.Script;
                    }
                    catch { }
                }

                if (witness_script is null) continue;
                networkFee += Wallet.CalculateNetworkFee(witness_script, ref size);
            }
            networkFee += size * policyAPI.GetFeePerByte();
            return networkFee;
        }

        /// <summary>
        /// Add Signature
        /// </summary>
        /// <param name="key">The KeyPair to sign transction</param>
        /// <returns></returns>
        public TransactionManager AddSignature(KeyPair key)
        {
            var contract = Contract.CreateSignatureContract(key.PublicKey);
            AddSignItem(contract, key);
            return this;
        }

        /// <summary>
        /// Add Multi-Signature
        /// </summary>
        /// <param name="key">The KeyPair to sign transction</param>
        /// <param name="m">The least count of signatures needed for multiple signature contract</param>
        /// <param name="publicKeys">The Public Keys construct the multiple signature contract</param>
        public TransactionManager AddMultiSig(KeyPair key, int m, params ECPoint[] publicKeys)
        {
            Contract contract = Contract.CreateMultiSigContract(m, publicKeys);
            AddSignItem(contract, key);
            return this;
        }

        private void AddSignItem(Contract contract, KeyPair key)
        {
            if (!Tx.GetScriptHashesForVerifying(null).Contains(contract.ScriptHash))
            {
                throw new Exception($"Add SignItem error: Mismatch ScriptHash ({contract.ScriptHash.ToString()})");
            }

            SignItem item = signStore.FirstOrDefault(p => p.Contract.ScriptHash == contract.ScriptHash);
            if (item is null)
            {
                signStore.Add(new SignItem { Contract = contract, KeyPairs = new HashSet<KeyPair> { key } });
            }
            else if (!item.KeyPairs.Contains(key))
            {
                item.KeyPairs.Add(key);
            }
        }

        /// <summary>
        /// Add Witness with contract
        /// </summary>
        /// <param name="contract">The witness verification contract</param>
        /// <param name="parameters">The witness invocation parameters</param>
        public TransactionManager AddWitness(Contract contract, params object[] parameters)
        {
            if (!context.Add(contract, parameters))
            {
                throw new Exception("AddWitness failed!");
            };
            return this;
        }

        /// <summary>
        /// Add Witness with scriptHash
        /// </summary>
        /// <param name="scriptHash">The witness verification contract hash</param>
        /// <param name="parameters">The witness invocation parameters</param>
        public TransactionManager AddWitness(UInt160 scriptHash, params object[] parameters)
        {
            var contract = Contract.Create(scriptHash);
            return AddWitness(contract, parameters);
        }

        /// <summary>
        /// Verify Witness count and add witnesses
        /// </summary>
        public TransactionManager Sign()
        {
            // Calculate NetworkFee
            Tx.NetworkFee = CalculateNetworkFee();
            var gasBalance = nep5API.BalanceOf(NativeContract.GAS.Hash, sender);
            if (gasBalance < Tx.SystemFee + Tx.NetworkFee)
                throw new InvalidOperationException($"Insufficient GAS in address: {sender.ToAddress()}");

            // Sign with signStore
            foreach (var item in signStore)
                foreach (var key in item.KeyPairs)
                {
                    byte[] signature = Tx.Sign(key);
                    if (!context.AddSignature(item.Contract, key.PublicKey, signature))
                    {
                        throw new Exception("AddSignature failed!");
                    }
                }

            // Verify witness count
            if (!context.Completed)
            {
                throw new Exception($"Please add signature or witness first!");
            }
            Tx.Witnesses = context.GetWitnesses();
            return this;
        }
    }
}
