using System;

namespace Neo.FileStorage.Morph.Invoker
{
    public partial class MorphContractInvoker
    {
        private static UInt160 BalanceContractHash => Settings.Default.BalanceContractHash;
        private const string BalanceOfMethod = "balanceOf";
        private const string DecimalsMethod = "decimals";

        public static long InvokeBalanceOf(IClient client, byte[] holder)
        {
            InvokeResult result = client.InvokeLocalFunction(BalanceContractHash, BalanceOfMethod, holder);
            if (result.State != VM.VMState.HALT) throw new Exception(string.Format("could not perform test invocation ({0})", BalanceOfMethod));
            return (long)result.ResultStack[0].GetInteger();
        }

        public static long InvokeDecimals(IClient client)
        {
            InvokeResult result = client.InvokeLocalFunction(BalanceContractHash, DecimalsMethod);
            if (result.State != VM.VMState.HALT) throw new Exception(string.Format("could not perform test invocation ({0})", DecimalsMethod));
            return (long)(result.ResultStack[0].GetInteger());
        }
    }
}
