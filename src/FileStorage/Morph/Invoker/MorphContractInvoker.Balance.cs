using System;

namespace Neo.FileStorage.Morph.Invoker
{
    public partial class MorphContractInvoker
    {
        private static UInt160 BalanceContractHash => Settings.Default.BalanceContractHash;
        private const string BalanceOfMethod = "balanceOf";
        private const string DecimalsMethod = "decimals";
        private const string TransferXMethod = "transferX";

        public static long InvokeBalanceOf(Client client, byte[] holder)
        {
            InvokeResult result = client.TestInvoke(BalanceContractHash, BalanceOfMethod, holder);
            if (result.State != VM.VMState.HALT) throw new Exception(string.Format("could not perform test invocation ({0})", BalanceOfMethod));
            return (long)result.ResultStack[0].GetInteger();
        }

        public static long InvokeDecimals(Client client)
        {
            InvokeResult result = client.TestInvoke(BalanceContractHash, DecimalsMethod);
            if (result.State != VM.VMState.HALT) throw new Exception(string.Format("could not perform test invocation ({0})", DecimalsMethod));
            return (long)(result.ResultStack[0].GetInteger());
        }

        public static bool InvokeTransferX(Client client, byte[] from, byte[] to, long amount, byte[] details)
        {
            return client.Invoke(out _, BalanceContractHash, TransferXMethod, ExtraFee, from, to, amount, details);
        }
    }
}
