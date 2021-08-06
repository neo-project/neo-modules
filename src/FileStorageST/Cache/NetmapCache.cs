using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Storage.Core;
using Neo.FileStorage.Invoker.Morph;
using Neo.FileStorage.Reputation;

namespace Neo.FileStorage.Storage.Cache
{
    public class NetmapCache : INetmapSource
    {
        public const int NetmapCacheSize = 10;

        private readonly IEpochSource epochSource;
        private readonly NetworkCache<ulong, NetMap> cache;

        public NetmapCache(IEpochSource epochSource, MorphInvoker morph)
        {
            this.epochSource = epochSource;
            cache = new(NetmapCacheSize, epoch =>
            {
                return morph.GetNetMapByEpoch(epoch);
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
