using Neo.SmartContract.Native;
using Neo.VM;
using System.Linq;
using System.Threading.Tasks;

namespace Neo.Network.RPC
{
    /// <summary>
    /// Get Policy info by RPC API
    /// </summary>
    public class PolicyAPI : ContractClient
    {
        readonly UInt160 scriptHash = NativeContract.Policy.Hash;

        /// <summary>
        /// PolicyAPI Constructor
        /// </summary>
        /// <param name="rpcClient">the RPC client to call NEO RPC methods</param>
        public PolicyAPI(RpcClient rpcClient) : base(rpcClient) { }

        /// <summary>
        /// Get Max Transactions Count Per Block
        /// </summary>
        /// <returns></returns>
        public async Task<uint> GetMaxTransactionsPerBlock()
        {
            var result = await TestInvoke(scriptHash, "getMaxTransactionsPerBlock").ConfigureAwait(false);
            return (uint)result.Stack.Single().GetInteger();
        }

        /// <summary>
        /// Get Max Block Size
        /// </summary>
        /// <returns></returns>
        public async Task<uint> GetMaxBlockSize()
        {
            var result = await TestInvoke(scriptHash, "getMaxBlockSize").ConfigureAwait(false);
            return (uint)result.Stack.Single().GetInteger();
        }

        /// <summary>
        /// Get Network Fee Per Byte
        /// </summary>
        /// <returns></returns>
        public async Task<long> GetFeePerByte()
        {
            var result = await TestInvoke(scriptHash, "getFeePerByte").ConfigureAwait(false);
            return (long)result.Stack.Single().GetInteger();
        }

        /// <summary>
        /// Get Ploicy Blocked Accounts
        /// </summary>
        /// <returns></returns>
        public async Task<UInt160[]> GetBlockedAccounts()
        {
            var result = await TestInvoke(scriptHash, "getBlockedAccounts").ConfigureAwait(false);
            var array = (VM.Types.Array)result.Stack.Single();
            return array.Select(p => new UInt160(p.GetSpan().ToArray())).ToArray();
        }
    }
}
