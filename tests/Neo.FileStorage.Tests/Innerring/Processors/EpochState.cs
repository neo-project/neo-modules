using Neo.FileStorage.InnerRing.Processors;

namespace Neo.FileStorage.Tests.InnerRing.Processors
{
    public class EpochState : IEpochState
    {
        public ulong EpochCounter()
        {
            return 0; ;
        }

        public void SetEpochCounter(ulong epoch)
        {
            return;
        }
    }
}
