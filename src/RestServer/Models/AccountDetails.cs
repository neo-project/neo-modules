using System.Numerics;

namespace Neo.Plugins.RestServer.Models
{
    public class AccountDetails
    {
        public UInt160 Account { get; set; }
        public string WalletAddress { get; set; }
        public BigInteger Balance { get; set; }
    }
}
