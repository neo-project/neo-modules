using Neo.FileStorage.API.Netmap;

namespace Neo.FileStorage.Reputation
{
    public interface INetmapSource
    {
        NetMap GetNetMapByDiff(ulong diff);
        NetMap GetNetMapByEpoch(ulong epoch);
    }
}
