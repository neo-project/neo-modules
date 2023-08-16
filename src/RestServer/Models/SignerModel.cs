using Neo.Cryptography.ECC;
using Neo.Network.P2P.Payloads;

namespace Neo.Plugins.RestServer.Models
{
    public class SignerModel
    {
        public IEnumerable<WitnessRuleModel> Rules { get; set; }
        public UInt160 Account { get; set; }
        public IEnumerable<UInt160> AllowedContracts { get; set; }
        public IEnumerable<ECPoint> AllowedGroups { get; set; }
        public WitnessScope Scopes { get; set; }
    }
}
