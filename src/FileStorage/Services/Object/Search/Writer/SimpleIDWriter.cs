
using System.Collections.Generic;
using Neo.FileStorage.API.Refs;

namespace Neo.FileStorage.Services.Object.Search.Writer
{
    public class SimpleIDWriter : ISearchResponseWriter
    {
        public List<ObjectID> IDs = new();

        public void WriteIDs(IEnumerable<ObjectID> ids)
        {
            IDs.AddRange(ids);
        }
    }
}
