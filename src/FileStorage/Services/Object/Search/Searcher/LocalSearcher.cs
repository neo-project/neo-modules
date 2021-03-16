using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.LocalObjectStorage.LocalStore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.FileStorage.Services.Object.Search.Searcher
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
