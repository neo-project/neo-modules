using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Cache;
using System.Collections.Generic;

namespace Neo.FileStorage.Storage.Services.Object.Put.Remote
{
    public class PutClientCache : IPutClientCache
    {
        private readonly IFSClientCache clientCache;

        public PutClientCache(IFSClientCache cache)
        {
            clientCache = cache;
        }

        public IPutClient Get(NodeInfo node)
        {
            return (IPutClient)clientCache.Get(node);
        }
    }
}
