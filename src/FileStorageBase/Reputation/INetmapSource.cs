using Neo.FileStorage.API.Netmap;

namespace Neo.FileStorage.Reputation
{
    public interface INetmapSource
    {
        NetMap GetNetMapByEpoch(ulong epoch);
    }
}
