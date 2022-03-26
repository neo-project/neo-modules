// Copyright (C) 2015-2021 The Neo Project.
//
// The Neo.Network.RPC is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Network.P2P.Payloads;
using Neo.Network.RPC.Models;
using System;
using System.Threading.Tasks;

namespace Neo.Network.RPC
{
    public class TransactionManagerFactory
    {
        private readonly RpcClient rpcClient;

        /// <summary>
        /// TransactionManagerFactory Constructor
        /// </summary>
        /// <param name="rpcClient">the RPC client to call NEO RPC API</param>
        public TransactionManagerFactory(RpcClient rpcClient)
        {
            this.rpcClient = rpcClient;
        }

        /// <summary>
        /// Create an unsigned Transaction object with given parameters.
        /// </summary>
        /// <param name="script">Transaction Script</param>
        /// <param name="attributes">Transaction Attributes</param>
        /// <returns></returns>
        public async Task<TransactionManager> MakeTransactionAsync(byte[] script, Signer[] signers = null, TransactionAttribute[] attributes = null)
        {
            RpcInvokeResult invokeResult = await rpcClient.InvokeScriptAsync(script, signers).ConfigureAwait(false);
            return await MakeTransactionAsync(script, invokeResult.GasConsumed, signers, attributes).ConfigureAwait(false);
        }

        public async Task<TransactionManager> MakeTransactionAsync(byte[] script, long systemFee, Signer[] signers = null, TransactionAttribute[] attributes = null)
        {
            uint blockCount = await rpcClient.GetBlockCountAsync().ConfigureAwait(false) - 1;

            var tx = new Transaction
            {
                Version = 0,
                Nonce = (uint)new Random().Next(),
                Script = script,
                Signers = signers ?? Array.Empty<Signer>(),
                ValidUntilBlock = blockCount - 1 + rpcClient.protocolSettings.MaxValidUntilBlockIncrement,
                SystemFee = systemFee,
                Attributes = attributes ?? Array.Empty<TransactionAttribute>(),
            };

            return new TransactionManager(tx, rpcClient);
        }
    }
}
