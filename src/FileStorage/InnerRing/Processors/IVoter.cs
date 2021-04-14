using Neo.Cryptography.ECC;

namespace Neo.Plugins.Innerring.Processors
{
    public interface IVoter
    {
        void VoteForSidechainValidator(ECPoint[] keys);
    }
}
