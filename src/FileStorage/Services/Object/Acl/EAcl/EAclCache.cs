
using System;
using Google.Protobuf;
using Microsoft.Extensions.Caching.Memory;
using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Cache;
using Neo.FileStorage.Morph.Invoker;

namespace Neo.FileStorage.Services.Object.Acl
{
    public class EAclCache
    {
        public const int DefaultCacheSize = 100;
        public const int DefaultTTLMilliseconds = 30000;

        private readonly TTLCache<ContainerID, EACLTable> cache;

        public EAclCache(Client client, int size = DefaultCacheSize, int ttl = DefaultTTLMilliseconds)
        {
            cache = new(size, TimeSpan.FromMilliseconds(ttl), cid =>
             {
                 var result = MorphContractInvoker.InvokeGetEACL(client, cid.ToByteArray());//TODO: invoke arge type and return use EAclWithSignature
                 var signature = Signature.Parser.ParseFrom(result.sig);
                 var eacl = EACLTable.Parser.ParseFrom(result.eacl);
                 if (!signature.VerifyMessagePart(eacl))
                     throw new InvalidOperationException("incorrect signature of eacl from morph client");
                 return eacl;
             });
        }

        public EACLTable GetEACL(ContainerID cid)
        {
            return cache.Get(cid);
        }

    }
}
