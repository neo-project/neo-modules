using NeoFS.API.v2.Acl;
using NeoFS.API.v2.Session;
using V2Object = NeoFS.API.v2.Object.Object;
using Neo.FSNode.Network.Cache;
using Neo.FSNode.Services.Object.Util;
using System;
using System.Threading;

namespace Neo.FSNode.Services.Object.Put.Store
{
    public class RemoteStore : IStore
    {
        public KeyStorage KeyStorage;
        public ClientCache ClientCache;
        public Network.Address Node;
        public SessionToken SessionToken;
        public BearerToken BearerToken;

        public void Put(V2Object obj)
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
            var oid = client.PutObject(source.Token, new NeoFS.API.v2.Client.ObjectParams.PutObjectParams { Object = obj }, new NeoFS.API.v2.Client.CallOptions
            {
                Ttl = 1,
                Session = SessionToken,
                Bearer = BearerToken,
            }).Result;
            if (oid is null)
                throw new InvalidOperationException(nameof(Range) + $" could not read object payload range from {addr}");
        }
    }
}
