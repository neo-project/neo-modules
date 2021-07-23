using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Cache;
using Neo.FileStorage.Invoker.Morph;
using Neo.FileStorage.Reputation;

namespace Neo.FileStorage.Storage.Cache
{
    public class NetmapCache : INetmapSource
    {
        public const int NetmapCacheSize = 10;

        private readonly StorageService storageService;
        private readonly NetworkCache<ulong, NetMap> cache;

        public NetmapCache(StorageService local, MorphInvoker morph)
        {
            storageService = local;
            cache = new(NetmapCacheSize, epoch =>
            {
                return morph.GetNetMapByEpoch(epoch);
            });
        }

        public NetMap GetNetMap(ulong diff)
        {
            return GetNetMapByEpoch(storageService.CurrentEpoch - diff);
        }

        public NetMap GetNetMapByEpoch(ulong epoch)
        {
            return cache.Get(epoch);
        }
    }
}
