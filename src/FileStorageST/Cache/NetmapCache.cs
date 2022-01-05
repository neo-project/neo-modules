using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Reputation;

namespace Neo.FileStorage.Storage.Cache
{
    public class NetmapCache : INetmapSource
    {
        public const int DefaultCapacity = 10;

        private readonly IEpochSource epochSource;
        private readonly NetworkCache<ulong, NetMap> cache;

        public NetmapCache(int cap, IEpochSource epochSource, INetmapSource netmapSource)
        {
            this.epochSource = epochSource;
            cache = new(cap > 0 ? cap : DefaultCapacity, epoch =>
            {
                return netmapSource.GetNetMapByEpoch(epoch);
            });
        }

        public NetMap GetNetMapByDiff(ulong diff)
        {
            return GetNetMapByEpoch(epochSource.CurrentEpoch - diff);
        }

        public NetMap GetNetMapByEpoch(ulong epoch)
        {
            return cache.Get(epoch);
        }
    }
}
