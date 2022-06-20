using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Cache;
using System.Collections.Generic;

namespace Neo.FileStorage.Storage.Services.Object.Get.Remote
{
    public class GetClientCache : IGetClientCache
    {
        private readonly IFSClientCache clientCache;

        public GetClientCache(IFSClientCache cache)
        {
            clientCache = cache;
        }

        public IGetClient Get(NodeInfo node)
        {
            return (IGetClient)clientCache.Get(node);
        }
    }
}
