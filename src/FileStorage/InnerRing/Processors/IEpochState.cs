namespace Neo.FileStorage.InnerRing.Processors
{
    public interface IEpochState
    {
        void SetEpochCounter(ulong epoch);
        ulong EpochCounter();
    }
}
