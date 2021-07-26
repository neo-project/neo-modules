using System.Numerics;
using Neo.FileStorage.API.Refs;

namespace Neo.FileStorage.InnerRing.Processors
{
    public class TransferTx
    {
        public OwnerID From;
        public OwnerID To;
        public BigInteger Amount;
    }
}
