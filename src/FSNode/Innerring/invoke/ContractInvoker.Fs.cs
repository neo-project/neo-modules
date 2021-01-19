using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Plugins.FSStorage.morph.invoke;
using Neo.VM.Types;
using System.Collections.Generic;
using System.Linq;
using Array = Neo.VM.Types.Array;

namespace Neo.Plugins.FSStorage.innerring.invoke
{
    public partial class ContractInvoker
    {
        private static UInt160 FsContractHash => Settings.Default.FsContractHash;
        private const string CheckIsInnerRingMethod = "isInnerRing";
        private const string ChequeMethod = "cheque";
        private const string InnerRingListMethod = "innerRingList";

        private const long FeeHalfGas = 50_000_000;
        private const long FeeOneGas = FeeHalfGas * 2;

        public class ChequeParams
        {
            public byte[] Id;
            public long Amount;
            public UInt160 UserAccount;
            public UInt160 LockAccount;
        }

        public static bool IsInnerRing(IClient client, ECPoint p)
        {
            InvokeResult result = client.InvokeLocalFunction(FsContractHash, CheckIsInnerRingMethod, p.EncodePoint(true));
            return result.ResultStack[0].GetBoolean();
        }

        public static bool CashOutCheque(IClient client, ChequeParams p)
        {
            return client.InvokeFunction(FsContractHash, ChequeMethod, ExtraFee, p.Id, p.UserAccount, p.Amount, p.LockAccount);
        }

        public static int InnerRingIndex(IClient client, ECPoint p,out int size)
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
