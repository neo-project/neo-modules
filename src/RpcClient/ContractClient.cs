using Neo.Network.P2P.Payloads;
using Neo.Network.RPC.Models;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using System.Threading.Tasks;

namespace Neo.Network.RPC
{
    /// <summary>
    /// Contract related operations through RPC API
    /// </summary>
    public class ContractClient
    {
        protected readonly RpcClient rpcClient;
        protected readonly uint? magic;


        /// <summary>
        /// ContractClient Constructor
        /// </summary>
        /// <param name="rpc">the RPC client to call NEO RPC methods</param>
        /// <param name="magic">
        /// the network Magic value to use when signing transactions. 
        /// Defaults to ProtocolSettings.Default.Magic if not specified.
        /// </param>
        public ContractClient(RpcClient rpc, uint? magic = null)
        {
            rpcClient = rpc;
            this.magic = magic;
        }

        /// <summary>
        /// Use RPC method to test invoke operation.
        /// </summary>
        /// <param name="scriptHash">contract script hash</param>
        /// <param name="operation">contract operation</param>
        /// <param name="args">operation arguments</param>
        /// <returns></returns>
        public Task<RpcInvokeResult> TestInvokeAsync(UInt160 scriptHash, string operation, params object[] args)
        {
            byte[] script = scriptHash.MakeScript(operation, args);
            return rpcClient.InvokeScriptAsync(script);
        }

        /// <summary>
        /// Deploy Contract, return signed transaction
        /// </summary>
        /// <param name="contractScript">contract script</param>
        /// <param name="manifest">contract manifest</param>
        /// <param name="key">sender KeyPair</param>
        /// <returns></returns>
        public async Task<Transaction> CreateDeployContractTxAsync(byte[] nefFile, ContractManifest manifest, KeyPair key)
        {
            byte[] script;
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitAppCall(NativeContract.ContractManagement.Hash, "deploy", nefFile, manifest.ToString());
                script = sb.ToArray();
            }
            UInt160 sender = Contract.CreateSignatureRedeemScript(key.PublicKey).ToScriptHash();
            Signer[] signers = new[] { new Signer { Scopes = WitnessScope.CalledByEntry, Account = sender } };

            TransactionManagerFactory factory = new TransactionManagerFactory(rpcClient, magic);
            TransactionManager manager = await factory.MakeTransactionAsync(script, signers).ConfigureAwait(false);
            return await manager
                .AddSignature(key)
                .SignAsync().ConfigureAwait(false);
        }
    }
}
