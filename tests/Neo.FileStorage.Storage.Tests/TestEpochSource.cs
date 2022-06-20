namespace Neo.FileStorage.Storage.Tests
{
    public class TestEpochSource : IEpochSource
    {
        public ulong Epoch;

        public ulong CurrentEpoch => Epoch;
    }
}
