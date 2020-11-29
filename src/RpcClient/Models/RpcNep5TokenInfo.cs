using System.Numerics;

namespace Neo.Network.RPC.Models
{
    public class RpcNep17TokenInfo
    {
        public string Name { get; set; }

        public string Symbol { get; set; }

        public byte Decimals { get; set; }

        public BigInteger TotalSupply { get; set; }
    }
}
