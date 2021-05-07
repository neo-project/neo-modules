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
            uint blockCount = await rpcClient.GetBlockCountAsync().ConfigureAwait(false) - 1;
            RpcInvokeResult invokeResult = await rpcClient.InvokeScriptAsync(script, signers).ConfigureAwait(false);

            var tx = new Transaction
            {
                Version = 0,
                Nonce = (uint)new Random().Next(),
                Script = script,
                Signers = signers ?? Array.Empty<Signer>(),
                ValidUntilBlock = blockCount - 1 + rpcClient.protocolSettings.MaxValidUntilBlockIncrement,
                SystemFee = invokeResult.GasConsumed,
                Attributes = attributes ?? Array.Empty<TransactionAttribute>(),
            };

            return new TransactionManager(tx, rpcClient);
        }
    }
}
