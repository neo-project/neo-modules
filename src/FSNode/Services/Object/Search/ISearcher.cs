using NeoFS.API.v2.Object;
using NeoFS.API.v2.Refs;
using System.Collections.Generic;

namespace Neo.FSNode.Services.Object.Search
{
    public interface ISearcher
    {
        List<ObjectID> Search(ContainerID cid, SearchFilters Filters);
    }
}
