using System.Numerics;

namespace Neo.Plugins.RestServer.Models
{
    public class TransactionTransferModel
    {
        public UInt160 TokenHash { get; set; }
        public string TokenName { get; set; }
        public string TokenSymbol { get; set; }
        public byte TokenDecimals { get; set; }
        public UInt160 To { get; set; }
        public UInt160 From { get; set; }
        public BigInteger Amount { get; set; }
    }
}
