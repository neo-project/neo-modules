using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using System.Collections.Generic;

namespace Neo.FileStorage.Storage.Services.Object.Search
{
    public interface ILocalSearchSource
    {
        List<Address> Select(ContainerID cid, SearchFilters filters);
    }
}
