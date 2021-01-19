namespace Neo.Plugins.FSStorage.innerring.processors
{
    public interface IEpochState
    {
        void SetEpochCounter(ulong epoch);
        ulong EpochCounter();
    }
}
