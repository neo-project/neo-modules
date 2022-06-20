using Neo.FileStorage.Storage.Cache;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Invoker.Morph;
using Neo.FileStorage.Storage.Core.Container;
using Neo.FileStorage.API.Client;
using System;

namespace Neo.FileStorage.Storage.Services.Container.Cache
{
    public class ContainerCache : IContainerSource
    {
        private readonly TTLNetworkCache<ContainerID, ContainerWithSignature> cache;

        public ContainerCache(int size, TimeSpan ttl, MorphInvoker invoker)
        {
            cache = new(size, ttl, cid => invoker.GetContainer(cid));
        }

        public ContainerWithSignature GetContainer(ContainerID cid)
        {
            return cache.Get(cid);
        }

        public void InvalidateContainer(ContainerID cid)
        {
            cache.Remove(cid);
        }
    }
}
