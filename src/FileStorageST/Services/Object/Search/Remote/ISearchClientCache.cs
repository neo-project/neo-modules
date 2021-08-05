using System.Collections.Generic;

namespace Neo.FileStorage.Storage.Services.Object.Search.Remote
{
    public interface ISearchClientCache
    {
        ISearchClient Get(List<Network.Address> addresses);
    }
}
