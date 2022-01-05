using Neo.FileStorage.Storage.Cache;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Invoker.Morph;
using System;
using System.Collections.Generic;

namespace Neo.FileStorage.Storage.Services.Container.Cache
{
    public class ContainerListCache
    {
        private readonly TTLNetworkCache<OwnerID, List<ContainerID>> cache;

        public ContainerListCache(int size, TimeSpan ttl, MorphInvoker invoker)
        {
            cache = new(size, ttl, owner => invoker.ListContainers(owner));
        }

        public List<ContainerID> GetContainerList(OwnerID owner)
        {
            return cache.Get(owner);
        }

        public void InvalidateContainerList(OwnerID owner)
        {
            cache.Remove(owner);
        }

        public void InvalidateContainerListByCid(ContainerID cid)
        {
            foreach (var owner in cache.Keys())
            {
                foreach (var id in cache.Get(owner))
                {
                    if (id.Equals(cid)) cache.Remove(owner);
                }
            }
        }
    }
}
