using Neo.FileStorage.API.Refs;
using V2Address = Neo.FileStorage.API.Refs.Address;
using Neo.FileStorage.Core.Container;
using Neo.FileStorage.Core.Netmap;
using Neo.FileStorage.Network;
using Neo.FileStorage.Services.Object.Search.Searcher;
using Neo.FileStorage.Services.Object.Util;
using Neo.FileStorage.Services.ObjectManager.Placement;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Neo.FileStorage.API.Object;

namespace Neo.FileStorage.Services.Object.Search
{
    public class SearchService
    {
        private INetmapSource netmapSource;
        private IContainerSource containerSource;
        private ILocalAddressSource localAddressSource;

        public SearchPrm ToSearchPrm(SearchRequest request, Action<SearchResponse> handler)
        {
            return new();
        }

        public void Search(SearchPrm prm)
        {
            var traverser = PreparePlacementTraverser(prm);
            if (traverser is null)
                throw new InvalidOperationException(nameof(SearchService) + " could not prepare placement traverser");
            Finish(prm, traverser);
        }

        private Traverser PreparePlacementTraverser(SearchPrm prm)
        {
            var nm = netmapSource.GetLatestNetworkMap();
            if (nm is null)
                throw new InvalidOperationException(nameof(SearchService) + " could not get latest network map");
            var container = containerSource.Get(prm.CID);
            if (container is null)
                throw new InvalidOperationException(nameof(SearchService) + " could not get container");
            var builder = new NetworkMapBuilder(new NetworkMapSource(nm));
            if (prm.Local)
                builder = new LocalPlacementBuilder(new NetworkMapSource(nm), localAddressSource);
            var traverser = new Traverser
            {
                Builder = builder,
                Policy = container.PlacementPolicy,
                Address = new V2Address
                {
                    ContainerId = container.CalCulateAndGetId,
                },
                FlatSuccess = 1,
            };
            return traverser;
        }

        //TODO: optimize
        private List<ObjectID> Finish(SearchPrm prm, Traverser traverser)
        {
            var oids = new ConcurrentBag<ObjectID>();
            while (true)
            {
                var addrs = traverser.Next();
                if (addrs.Count == 0) break;
                var tasks = new List<Task>();
                foreach (var addr in addrs)
                {
                    tasks.Add(Task.Run(() =>
                    {
                        ISearcher searcher;
                        if (addr.IsLocalAddress(localAddressSource))
                        {
                            searcher = new LocalSearcher();
                        }
                        else
                        {
                            searcher = new RemoteSearcher();
                        }
                        var res = searcher.Search(prm.CID, prm.Filters);
                        oids = new ConcurrentBag<ObjectID>(oids.Union(res));
                    }));
                }
            }
            return oids.ToList();
        }
    }
}
