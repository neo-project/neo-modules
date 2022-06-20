using System.Collections.Generic;
using Neo.FileStorage.API.Netmap;

namespace Neo.FileStorage.Storage.Services.Object.Search.Remote
{
    public interface ISearchClientCache
    {
        ISearchClient Get(NodeInfo node);
    }
}
