using Neo.FileStorage.Storage.Cache;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Invoker.Morph;
using Neo.FileStorage.Storage.Services.Object.Acl.EAcl;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Acl;
using System;

namespace Neo.FileStorage.Storage.Services.Container.Cache
{
    public class EACLCache : IEACLSource
    {
        private readonly TTLNetworkCache<ContainerID, EAclWithSignature> cache;

        public EACLCache(int size, TimeSpan ttl, MorphInvoker invoker)
        {
            cache = new(size, ttl, cid => invoker.GetEACL(cid));
        }

        public EAclWithSignature GetEAclWithSignature(ContainerID cid)
        {
            return cache.Get(cid);
        }

        public EACLTable GetEACL(ContainerID cid)
        {
            return cache.Get(cid)?.Table;
        }

        public void InvalidateEACL(ContainerID cid)
        {
            cache.Remove(cid);
        }
    }
}
