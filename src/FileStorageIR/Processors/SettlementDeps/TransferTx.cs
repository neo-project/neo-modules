using System.Numerics;
using Neo.FileStorage.API.Refs;

namespace Neo.FileStorage.InnerRing.Processors
{
    public class TransferTx
    {
        public OwnerID from;
        public OwnerID to;
        public BigInteger amount;
    }
}
