
namespace Neo.Plugins.StateService.Verification
{
    public class Vote
    {
        public readonly int ValidatorIndex;
        public readonly uint RootIndex;
        public readonly byte[] Signature;

        public Vote(uint index, int validator, byte[] signature)
        {
            RootIndex = index;
            ValidatorIndex = validator;
            Signature = signature;
        }
    }
}
