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
using System.Threading.Tasks;

namespace Neo.Network.RPC
{
    /// <summary>
    /// This class helps to create transaction with RPC API.
    /// </summary>
    public class TransactionManager
    {
        private class SignItem { public Contract Contract; public HashSet<KeyPair> KeyPairs; }

        private readonly RpcClient rpcClient;

        /// <summary>
        /// protocol settings Magic value to use for hashing transactions.
        /// defaults to ProtocolSettings.Default.Magic if unspecified
        /// </summary>
        private readonly uint magic;

        /// <summary>
        /// The Transaction context to manage the witnesses
        /// </summary>
        private ContractParametersContext context;

        /// <summary>
        /// This container stores the keys for sign the transaction
        /// </summary>
        private readonly List<SignItem> signStore = new List<SignItem>();

        // task to manage fluent async operations 
        private Task fluentOperationsTask = Task.FromResult(0);

        /// <summary>
        /// The Transaction managed by this class
        /// </summary>
        public Transaction Tx { get; private set; }

        /// <summary>
        /// TransactionManager Constructor
        /// </summary>
        /// <param name="rpc">the RPC client to call NEO RPC API</param>
        public TransactionManager(RpcClient rpcClient, uint? magic = null)
        {
            this.rpcClient = rpcClient;
            this.magic = magic ?? ProtocolSettings.Default.Magic;
        }

        /// <summary>
        /// Create an unsigned Transaction object with given parameters.
        /// </summary>
        /// <param name="script">Transaction Script</param>
        /// <param name="attributes">Transaction Attributes</param>
        /// <returns></returns>
        public TransactionManager MakeTransaction(byte[] script, Signer[] signers = null, TransactionAttribute[] attributes = null)
        {
            Tx = new Transaction
            {
                Version = 0,
                Nonce = (uint)new Random().Next(),
                Script = script,
                Signers = signers ?? Array.Empty<Signer>(),
                Attributes = attributes ?? Array.Empty<TransactionAttribute>(),
            };

            context = new ContractParametersContext(Tx);

            QueueWork(async _ =>
            {
                uint height = await rpcClient.GetBlockCountAsync().ConfigureAwait(false) - 1;
                Tx.ValidUntilBlock = height + Transaction.MaxValidUntilBlockIncrement;
            });

            QueueWork(async _ =>
            {
                RpcInvokeResult result = await rpcClient.InvokeScriptAsync(script, signers).ConfigureAwait(false);
                Tx.SystemFee = long.Parse(result.GasConsumed);
            });

            return this;
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

        /// <summary>
        /// Add Multi-Signature
        /// </summary>
        /// <param name="keys">The KeyPairs to sign transction</param>
        /// <param name="m">The least count of signatures needed for multiple signature contract</param>
        /// <param name="publicKeys">The Public Keys construct the multiple signature contract</param>
        public TransactionManager AddMultiSig(KeyPair[] keys, int m, params ECPoint[] publicKeys)
        {
            Contract contract = Contract.CreateMultiSigContract(m, publicKeys);
            for (int i = 0; i < keys.Length; i++)
            {
                AddSignItem(contract, keys[i]);
            }
            return this;
        }

        /// <summary>
        /// Add Witness with contract
        /// </summary>
        /// <param name="contract">The witness verification contract</param>
        /// <param name="parameters">The witness invocation parameters</param>
        public TransactionManager AddWitness(Contract contract, params object[] parameters)
        {
            QueueWork(() =>
            {
                if (!context.Add(contract, parameters))
                {
                    throw new Exception("AddWitness failed!");
                };
            });
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
        public async Task<Transaction> SignAsync()
        {
            // wait for all queued work to complete
            await fluentOperationsTask;

            // Calculate NetworkFee
            Tx.Witnesses = Tx.GetScriptHashesForVerifying(null).Select(u => new Witness()
            {
                InvocationScript = Array.Empty<byte>(),
                VerificationScript = GetVerificationScript(u)
            }).ToArray();
            Tx.NetworkFee = await rpcClient.CalculateNetworkFeeAsync(Tx).ConfigureAwait(false);
            Tx.Witnesses = null;

            var gasBalance = await new Nep5API(rpcClient).BalanceOfAsync(NativeContract.GAS.Hash, Tx.Sender).ConfigureAwait(false);
            if (gasBalance < Tx.SystemFee + Tx.NetworkFee)
                throw new InvalidOperationException($"Insufficient GAS in address: {Tx.Sender.ToAddress()}");

            // Sign with signStore
            for (int i = 0; i < signStore.Count; i++)
            {
                SignItem item = signStore[i];
                foreach (var key in item.KeyPairs)
                {
                    byte[] signature = Tx.Sign(key, magic);
                    if (!context.AddSignature(item.Contract, key.PublicKey, signature))
                    {
                        throw new Exception("AddSignature failed!");
                    }
                }
            }

            // Verify witness count
            if (!context.Completed)
            {
                throw new Exception($"Please add signature or witness first!");
            }
            Tx.Witnesses = context.GetWitnesses();
            return Tx;
        }

        private void AddSignItem(Contract contract, KeyPair key)
        {
            QueueWork(() =>
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
            });
        }

        private void QueueWork(Func<Task, Task> action)
        {
            fluentOperationsTask = fluentOperationsTask.ContinueWith(action, TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        private void QueueWork(Action action)
        {
            QueueWork(_ =>
            {
                action();
                return Task.FromResult(0);
            });
        }

        private byte[] GetVerificationScript(UInt160 hash)
        {
            foreach (var item in signStore)
            {
                if (item.Contract.ScriptHash == hash) return item.Contract.Script;
            }

            return Array.Empty<byte>();
        }
    }
}
