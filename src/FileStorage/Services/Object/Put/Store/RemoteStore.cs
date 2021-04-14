using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Session;
using V2Object = Neo.FileStorage.API.Object.Object;
using Neo.FileStorage.Network.Cache;
using Neo.FileStorage.Services.Object.Util;
using System;
using System.Threading;

namespace Neo.FileStorage.Services.Object.Put.Store
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
            var oid = client.PutObject(new Neo.FileStorage.API.Client.ObjectParams.PutObjectParams { Object = obj }, new Neo.FileStorage.API.Client.CallOptions
            {
                Ttl = 1,
                Session = SessionToken,
                Bearer = BearerToken,
            }, source.Token).Result;
            if (oid is null)
                throw new InvalidOperationException(nameof(Range) + $" could not read object payload range from {addr}");
        }
    }
}
