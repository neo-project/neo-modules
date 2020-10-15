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
        /// protocol settings Magic value to use for hashing transactions.
        /// defaults to ProtocolSettings.Default.Magic if unspecified
        /// </summary>
        private readonly uint magic;

        /// <summary>
        /// TransactionManagerFactory Constructor
        /// </summary>
        /// <param name="rpcClient">the RPC client to call NEO RPC API</param>
        /// <param name="magic">
        /// the network Magic value to use when signing transactions. 
        /// Defaults to ProtocolSettings.Default.Magic if not specified.
        /// </param>
        public TransactionManagerFactory(RpcClient rpcClient, uint? magic = null)
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
                ValidUntilBlock = blockCount - 1 + Transaction.MaxValidUntilBlockIncrement,
                SystemFee = long.Parse(invokeResult.GasConsumed),
                Attributes = attributes ?? Array.Empty<TransactionAttribute>(),
            };

            return new TransactionManager(tx, rpcClient, magic);
        }
    }
}
