using NeoFS.API.v2.Acl;
using NeoFS.API.v2.Refs;
using NeoFS.API.v2.Session;
using V2Object = NeoFS.API.v2.Object.Object;
using Neo.FSNode.Network.Cache;
using Neo.FSNode.Services.Object.Util;
using System;

namespace Neo.FSNode.Services.Object.Head.HeaderSource
{
    public class RemoteHeaderSource : IHeaderSource
    {
        public KeyStorage KeyStorage;
        public ClientCache ClientCache;
        public Network.Address Node;
        public SessionToken SessionToken;
        public BearerToken BearerToken;

        public V2Object Head(Address address)
        {
            var key = KeyStorage.GetKey(SessionToken);
            if (key is null)
                throw new InvalidOperationException(nameof(Range) + " could not receive private key");
            var addr = Node.IPAddressString();
            var client = ClientCache.GetClient(key, addr);
            if (client is null)
                throw new InvalidOperationException(nameof(Range) + $" could not create SDK client {addr}");
            var header = client.GetObjectHeader(address, false, new NeoFS.API.v2.Client.CallOptions
            {
                Ttl = 1,
                Session = SessionToken,
                Bearer = BearerToken,
            });
            if (header is null)
                throw new InvalidOperationException(nameof(Range) + $" could not read object payload range from {addr}");
            return header;
        }
    }
}
