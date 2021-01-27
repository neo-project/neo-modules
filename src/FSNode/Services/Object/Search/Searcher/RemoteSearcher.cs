using NeoFS.API.v2.Object;
using NeoFS.API.v2.Client.ObjectParams;
using NeoFS.API.v2.Refs;
using Neo.FSNode.Network.Cache;
using Neo.FSNode.Services.Object.Util;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Neo.FSNode.Services.Object.Search.Searcher
{
    public class RemoteSearcher : ISearcher
    {
        private readonly KeyStorage keyStorage;
        private readonly ClientCache clientCache;
        private readonly Network.Address node;
        private readonly SearchPrm prm;

        public List<ObjectID> Search(ContainerID cid, SearchFilters filters)
        {
            var key = keyStorage.GetKey(prm.SessionToken);
            if (key is null)
                throw new InvalidOperationException(nameof(RemoteSearcher) + " could not receive private key");
            var addr = node.IPAddressString();
            var client = clientCache.GetClient(key, addr);
            if (client is null)
                throw new InvalidOperationException(nameof(RemoteSearcher) + $" could not create SDK client {addr}");
            var source = new CancellationTokenSource();
            source.CancelAfter(TimeSpan.FromMinutes(1));
            var oids = client.SearchObject(source.Token,
                new SearchObjectParams
                {
                    ContainerID = cid,
                    Filters = filters,
                },
                new NeoFS.API.v2.Client.CallOptions
                {
                    Ttl = 1,
                    Session = prm.SessionToken,
                    Bearer = prm.BearerToken,
                });
            if (oids is null)
                throw new InvalidOperationException(nameof(RemoteSearcher) + $" could not read range hash from {addr}");
            return oids.Result;
        }
    }
}
