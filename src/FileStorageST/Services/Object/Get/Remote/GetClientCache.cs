using System.Collections.Generic;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.Cache;

namespace Neo.FileStorage.Storage.Services.Object.Get.Remote
{
    public class GetClientCache : IGetClientCache
    {
        private readonly IFSClientCache clientCache;

        public GetClientCache(IFSClientCache cache)
        {
            clientCache = cache;
        }

        public IGetClient Get(List<Network.Address> addresses)
        {
            return (IGetClient)clientCache.Get(addresses);
        }
    }
}
