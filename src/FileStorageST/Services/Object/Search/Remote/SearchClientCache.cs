using Neo.FileStorage.Cache;
using Neo.FileStorage.Storage.Services.Object.Search.Remote;
using System.Collections.Generic;

namespace Neo.FileStorage.Storage.Services.Object.Search.Clients
{
    public class SearchClientCache : ISearchClientCache
    {
        private readonly IFSClientCache clientCache;

        public SearchClientCache(IFSClientCache cache)
        {
            clientCache = cache;
        }

        public ISearchClient Get(List<Network.Address> addresses)
        {
            return new SearchClient(clientCache.Get(addresses));
        }
    }
}
