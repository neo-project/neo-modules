using System.Collections.Generic;
using Neo.FileStorage.Cache;

namespace Neo.FileStorage.Storage.Services.Object.Put.Remote
{
    public class PutClientCache : IPutClientCache
    {
        private readonly IFSClientCache clientCache;

        public PutClientCache(IFSClientCache cache)
        {
            clientCache = cache;
        }

        public IPutClient Get(List<Network.Address> addresses)
        {
            return (IPutClient)clientCache.Get(addresses);
        }
    }
}
