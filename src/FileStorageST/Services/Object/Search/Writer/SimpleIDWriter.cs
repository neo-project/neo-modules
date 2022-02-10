using Neo.FileStorage.API.Refs;
using System;
using System.Collections.Generic;

namespace Neo.FileStorage.Storage.Services.Object.Search.Writer
{
    public class SimpleIDWriter : ISearchResponseWriter
    {
        public List<ObjectID> IDs = new();

        public void WriteIDs(IEnumerable<ObjectID> ids)
        {
            if (ids is null) throw new ArgumentNullException(nameof(ids));
            IDs.AddRange(ids);
        }
    }
}
