
using Neo.FileStorage.API.Refs;
using System.Collections.Generic;

namespace Neo.FileStorage.Services.Object.Search
{
    public interface ISearchResponseWriter
    {
        void WriteIDs(IEnumerable<ObjectID> ids);
    }
}
