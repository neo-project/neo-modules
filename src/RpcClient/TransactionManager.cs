// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Network.RPC is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Network.P2P.Payloads;
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

        private readonly RpcClient _rpcClient;

        /// <summary>
        /// The Transaction context to manage the witnesses
        /// </summary>
        private readonly ContractParametersContext _context;

        /// <summary>
        /// This container stores the keys for sign the transaction
        /// </summary>
        private readonly List<SignItem> _signStore = new List<SignItem>();

        /// <summary>
        /// The Transaction managed by this instance
        /// </summary>
        private readonly Transaction _tx;

        public Transaction Tx => _tx;

        /// <summary>
        /// TransactionManager Constructor
        /// </summary>
        /// <param name="tx">the transaction to manage. Typically buildt</param>
        /// <param name="rpcClient">the RPC client to call NEO RPC API</param>
        public TransactionManager(Transaction tx, RpcClient rpcClient)
        {
            this._tx = tx;
            this._context = new ContractParametersContext(null, tx, rpcClient.ProtocolSettings.Network);
            this._rpcClient = rpcClient;
        }

        /// <summary>
        /// Helper function for one-off TransactionManager creation
        /// </summary>
        public static Task<TransactionManager> MakeTransactionAsync(RpcClient rpcClient, ReadOnlyMemory<byte> script, Signer[] signers = null, TransactionAttribute[] attributes = null)
        {
            var factory = new TransactionManagerFactory(rpcClient);
            return factory.MakeTransactionAsync(script, signers, attributes);
        }

        /// <summary>
        /// Helper function for one-off TransactionManager creation
        /// </summary>
        public static Task<TransactionManager> MakeTransactionAsync(RpcClient rpcClient, ReadOnlyMemory<byte> script, long systemFee, Signer[] signers = null, TransactionAttribute[] attributes = null)
        {
            var factory = new TransactionManagerFactory(rpcClient);
            return factory.MakeTransactionAsync(script, systemFee, signers, attributes);
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

        private void AddSignItem(Contract contract, KeyPair key)
        {
            if (!Tx.GetScriptHashesForVerifying(null).Contains(contract.ScriptHash))
            {
                throw new Exception($"Add SignItem error: Mismatch ScriptHash ({contract.ScriptHash})");
            }

            SignItem item = _signStore.FirstOrDefault(p => p.Contract.ScriptHash == contract.ScriptHash);
            if (item is null)
            {
                _signStore.Add(new SignItem { Contract = contract, KeyPairs = new HashSet<KeyPair> { key } });
            }
            else
                item.KeyPairs.Add(key);
        }

        /// <summary>
        /// Add Witness with contract
        /// </summary>
        /// <param name="contract">The witness verification contract</param>
        /// <param name="parameters">The witness invocation parameters</param>
        public TransactionManager AddWitness(Contract contract, params object[] parameters)
        {
            if (!_context.Add(contract, parameters))
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
        public async Task<Transaction> SignAsync()
        {
            // Calculate NetworkFee
            Tx.Witnesses = Tx.GetScriptHashesForVerifying(null).Select(u => new Witness()
            {
                InvocationScript = Array.Empty<byte>(),
                VerificationScript = GetVerificationScript(u)
            }).ToArray();
            Tx.NetworkFee = await _rpcClient.CalculateNetworkFeeAsync(Tx).ConfigureAwait(false);
            Tx.Witnesses = null;

            var gasBalance = await new Nep17API(_rpcClient).BalanceOfAsync(NativeContract.GAS.Hash, Tx.Sender).ConfigureAwait(false);
            if (gasBalance < Tx.SystemFee + Tx.NetworkFee)
                throw new InvalidOperationException($"Insufficient GAS in address: {Tx.Sender.ToAddress(_rpcClient.ProtocolSettings.AddressVersion)}");

            // Sign with signStore
            for (int i = 0; i < _signStore.Count; i++)
            {
                foreach (var key in _signStore[i].KeyPairs)
                {
                    byte[] signature = Tx.Sign(key, _rpcClient.ProtocolSettings.Network);
                    if (!_context.AddSignature(_signStore[i].Contract, key.PublicKey, signature))
                    {
                        throw new Exception("AddSignature failed!");
                    }
                }
            }

            // Verify witness count
            if (!_context.Completed)
            {
                throw new Exception($"Please add signature or witness first!");
            }
            Tx.Witnesses = _context.GetWitnesses();
            return Tx;
        }

        private byte[] GetVerificationScript(UInt160 hash)
        {
            foreach (var item in _signStore)
            {
                if (item.Contract.ScriptHash == hash) return item.Contract.Script;
            }

            return Array.Empty<byte>();
        }
    }
}
