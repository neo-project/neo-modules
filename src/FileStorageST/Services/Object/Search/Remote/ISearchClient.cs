using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Storage.Services.Object.Search.Execute;
using System.Collections.Generic;

namespace Neo.FileStorage.Storage.Services.Object.Search.Remote
{
    public interface ISearchClient
    {
        IEnumerable<ObjectID> SearchObjects(ExecuteContext context);
    }
}
