using Neo.FileStorage.API.Refs;
using System.Collections.Generic;

namespace Neo.FileStorage.Storage.Services.Object.Search
{
    public interface ISearchResponseWriter
    {
        void WriteIDs(IEnumerable<ObjectID> ids);
    }
}
