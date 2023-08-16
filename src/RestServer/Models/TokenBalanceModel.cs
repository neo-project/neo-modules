using System.Numerics;

namespace Neo.Plugins.RestServer.Models
{
    public class TokenBalanceModel
    {
        public string Name { get; set; }
        public UInt160 ScriptHash { get; set; }
        public string Symbol { get; set; }
        public byte Decimals { get; set; }
        public BigInteger Balance { get; set; }
        public BigInteger TotalSupply { get; set; }
    }
}
