using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Cache;
using Neo.FileStorage.Storage.Services.Object.Search.Remote;

namespace Neo.FileStorage.Storage.Services.Object.Search.Clients
{
    public class SearchClientCache : ISearchClientCache
    {
        private readonly IFSClientCache clientCache;

        public SearchClientCache(IFSClientCache cache)
        {
            clientCache = cache;
        }

        public ISearchClient Get(NodeInfo node)
        {
            return new SearchClient(clientCache.Get(node));
        }
    }
}
