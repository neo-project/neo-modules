using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.FileStorage.Storage.Services.Object.Search.Writer
{
    public class SearchResponseWriter : ISearchResponseWriter
    {
        public Action<SearchResponse> Handler { get; init; }
        private HashSet<ObjectID> written = new();

        public void WriteIDs(IEnumerable<ObjectID> ids)
        {
            var to_write = ids.Where(p => !written.Contains(p)).Distinct();
            Handler(SearchResponse(to_write));
            written = written.Concat(to_write).ToHashSet();
        }

        private SearchResponse SearchResponse(IEnumerable<ObjectID> ids)
        {
            SearchResponse.Types.Body body = new();
            body.IdList.AddRange(ids);
            SearchResponse resp = new();
            resp.Body = body;
            return resp;
        }
    }
}
