using Neo.Cryptography.ECC;

namespace Neo.FileStorage.InnerRing.Processors
{
    public interface IVoter
    {
        void VoteForSidechainValidator(ECPoint[] keys);
    }
}
