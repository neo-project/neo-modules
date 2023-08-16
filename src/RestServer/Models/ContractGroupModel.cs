using Neo.Cryptography.ECC;

namespace Neo.Plugins.RestServer.Models
{
    public class ContractGroupModel
    {
        public ECPoint PubKey { get; set; }
        public byte[] Signature { get; set; }
    }
}
