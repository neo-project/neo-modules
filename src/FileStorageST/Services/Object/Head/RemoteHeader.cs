using System;
using System.Threading;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.Storage.Services.Object.Util;
using Neo.FileStorage.Storage.Services.Reputaion.Local.Client;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.Services.Object.Head
{
    public class RemoteHeader
    {
        public const int DefaultHeadTtl = 1;
        public KeyStorage KeyStorage { get; init; }
        public ReputationClientCache ClientCache { get; init; }

        public FSObject Head(RemoteHeadPrm prm, CancellationToken context)
        {
            var key = KeyStorage.GetKey(prm.SessionToken);
            var client = ClientCache.Get(prm.Addresses);
            return client.GetObjectHeader(prm.Address, prm.Short, prm.Raw, new CallOptions
            {
                Ttl = DefaultHeadTtl,
                Session = prm.SessionToken,
                Bearer = prm.BearerToken,
                Key = key,
            }, context).Result;
        }
    }
}
