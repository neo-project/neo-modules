using Neo.Cryptography.ECC;

namespace Neo.Plugins.RestServer.Models
{
    public class ContractPermissionDescriptorModel
    {
        public ECPoint Group { get; set; }
        public UInt160 Hash { get; set; }
        public bool IsGroup { get; set; }
        public bool IsHash { get; set; }
        public bool IsWildcard { get; set; }
    }
}
