using System.Collections.Generic;
using Neo.Network.P2P.Payloads;

namespace Neo.Plugins
{
    public class NotaryRequestComparer : IComparer<NotaryRequest>
    {
        public int Compare(NotaryRequest x, NotaryRequest y)
        {
            var r = x.FallbackTransaction.FeePerByte.CompareTo(y.FallbackTransaction.FeePerByte);
            if (r != 0) return r;
            r = x.FallbackTransaction.NetworkFee.CompareTo(y.FallbackTransaction.NetworkFee);
            if (r != 0) return r;
            return x.FallbackTransaction.Hash.CompareTo(y.FallbackTransaction.Hash);
        }
    }
}
