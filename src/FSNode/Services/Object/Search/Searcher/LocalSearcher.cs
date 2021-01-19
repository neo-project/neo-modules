using NeoFS.API.v2.Object;
using NeoFS.API.v2.Refs;
using Neo.FSNode.LocalObjectStorage.LocalStore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.FSNode.Services.Object.Search.Searcher
{
    public class LocalSearcher : ISearcher
    {
        private readonly Storage localStorage;

        public List<ObjectID> Search(ContainerID cid, SearchFilters Filters)
        {
            Filters.AddObjectContainerIDFilter(MatchType.StringEqual, cid);
            var addrs = localStorage.Select(Filters);
            if (addrs is null)
                throw new InvalidOperationException(nameof(LocalSearcher) + " could not select objects from local storage");
            return addrs.Select(p => p.ObjectId).ToList();
        }
    }
}
