using Neo.SmartContract.Native;
using Neo.VM;
using System.Linq;
using System.Threading.Tasks;
using Neo.IO.Json;

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
        public async Task<uint> GetMaxTransactionsPerBlockAsync()
        {
            var result = await TestInvokeAsync(scriptHash, "getMaxTransactionsPerBlock").ConfigureAwait(false);
            return (uint)result.Stack.Single().GetInteger();
        }

        /// <summary>
        /// Get Max Block Size
        /// </summary>
        /// <returns></returns>
        public async Task<uint> GetMaxBlockSizeAsync()
        {
            var result = await TestInvokeAsync(scriptHash, "getMaxBlockSize").ConfigureAwait(false);
            return (uint)result.Stack.Single().GetInteger();
        }

        /// <summary>
        /// Get Network Fee Per Byte
        /// </summary>
        /// <returns></returns>
        public async Task<long> GetFeePerByteAsync()
        {
            var result = await TestInvokeAsync(scriptHash, "getFeePerByte").ConfigureAwait(false);
            return (long)result.Stack.Single().GetInteger();
        }

        /// <summary>
        /// Get Ploicy Blocked Accounts
        /// </summary>
        /// <returns></returns>
        public async Task<bool> IsBlockedAsync(UInt160 account)
        {
            var result = await TestInvokeAsync(scriptHash, "isBlocked", new object[] { account }).ConfigureAwait(false);
            return result.Stack.Single().GetBoolean();
        }
    }
}
