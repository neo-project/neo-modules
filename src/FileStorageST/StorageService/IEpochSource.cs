namespace Neo.FileStorage.Storage
{
    public interface IEpochSource
    {
        ulong CurrentEpoch { get; }
    }
}
