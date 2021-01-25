using NeoFS.API.v2.Acl;
using NeoFS.API.v2.Client;
using NeoFS.API.v2.Client.ObjectParams;
using NeoFS.API.v2.Refs;
using NeoFS.API.v2.Session;
using V2Object = NeoFS.API.v2.Object.Object;
using Neo.FSNode.Network.Cache;
using Neo.FSNode.Services.Object.Util;
using System;
using System.Threading;

namespace Neo.FSNode.Services.Object.Head.HeaderSource
{
    public class RemoteHeaderSource
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
            var source = new CancellationTokenSource();
            source.CancelAfter(TimeSpan.FromMinutes(1));
            var header = client.GetObjectHeader(source.Token, new ObjectHeaderParams { Address = address, Short = false }, new CallOptions
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
