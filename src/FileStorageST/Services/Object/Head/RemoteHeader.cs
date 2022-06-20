using Neo.FileStorage.API.Client;
using Neo.FileStorage.Storage.Services.Object.Util;
using Neo.FileStorage.Storage.Services.Reputaion.Client;
using System;
using System.Threading;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.Services.Object.Head
{
    public class RemoteHeader : IRemoteHeader
    {
        public const int DefaultHeadTtl = 1;
        public KeyStore KeyStorage { get; init; }
        public ReputationClientCache ClientCache { get; init; }

        public FSObject Head(RemoteHeadPrm prm, CancellationToken cancellation)
        {
            var key = KeyStorage.GetKey(prm.SessionToken);
            var client = ClientCache.Get(prm.Node);
            return client.GetObjectHeader(prm.Address, prm.Short, prm.Raw, new CallOptions
            {
                Ttl = DefaultHeadTtl,
                Session = prm.SessionToken,
                Bearer = prm.BearerToken,
                Key = key,
            }, cancellation).Result;
        }
    }
}
