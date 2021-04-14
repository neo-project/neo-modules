using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using System.Collections.Generic;

namespace Neo.FileStorage.Services.Object.Search
{
    public interface ISearcher
    {
        List<ObjectID> Search(ContainerID cid, SearchFilters Filters);
    }
}
