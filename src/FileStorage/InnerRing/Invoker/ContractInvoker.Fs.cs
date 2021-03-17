using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.FileStorage.Morph.Invoker;
using Neo.VM.Types;
using System.Collections.Generic;
using System.Linq;
using Array = Neo.VM.Types.Array;

namespace Neo.FileStorage.InnerRing.Invoker
{
    public partial class ContractInvoker
    {
        private static UInt160 FsContractHash => Settings.Default.FsContractHash;
        private const string CheckIsInnerRingMethod = "isInnerRing";
        private const string ChequeMethod = "cheque";
        private const string InnerRingListMethod = "innerRingList";

        private const long FeeHalfGas = 50_000_000;
        private const long FeeOneGas = FeeHalfGas * 2;

        public static bool IsInnerRing(IClient client, ECPoint p)
        {
            InvokeResult result = client.InvokeLocalFunction(FsContractHash, CheckIsInnerRingMethod, p.EncodePoint(true));
            return result.ResultStack[0].GetBoolean();
        }

        public static bool CashOutCheque(IClient client, byte[] Id, long amount, UInt160 userAccount, UInt160 lockAccount)
        {
            return client.InvokeFunction(FsContractHash, ChequeMethod, ExtraFee, Id, userAccount, amount, lockAccount);
        }

        public static int InnerRingIndex(IClient client, ECPoint p, out int size)
        {
            InvokeResult result = client.InvokeLocalFunction(FsContractHash, InnerRingListMethod);
            var irNodes = (Array)result.ResultStack[0];
            size = irNodes.Count();
            IEnumerator<StackItem> enumerator = irNodes.GetEnumerator();
            var index = -1;
            var i = -1;
            while (enumerator.MoveNext())
            {
                i++;
                var key = (Array)enumerator.Current;
                var keyValue = key[0].GetSpan().ToArray();
                if (p.ToArray().ToHexString().Equals(keyValue.ToHexString()))
                {
                    index = i;
                    break;
                }
            }
            return index;
        }
    }
}
