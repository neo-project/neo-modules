using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Cache;
using Neo.FileStorage.Morph.Invoker;

namespace Neo.FileStorage.Storage
{
    public class NetmapCache
    {
        public const int NetmapCacheSize = 10;

        private readonly StorageService storageService;
        private readonly NetworkCache<ulong, NetMap> cache;

        public NetmapCache(StorageService local, Client morph)
        {
            storageService = local;
            cache = new(NetmapCacheSize, epoch =>
            {
                return morph.InvokeEpochSnapshot(epoch);
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
