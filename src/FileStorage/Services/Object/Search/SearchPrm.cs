using System;
using System.Collections.Generic;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Services.Object.Util;

namespace Neo.FileStorage.Services.Object.Search
{
    public class SearchPrm : CommonPrm
    {
        public ContainerID ContainerID;
        public SearchFilters Filters;
        public ISearchResponseWriter Writer;
        public Func<Client, List<ObjectID>> Forwarder;
        public static SearchPrm FromRequest(SearchRequest request)
        {
            var prm = new SearchPrm
            {
                ContainerID = request.Body.ContainerId,
                Filters = new SearchFilters(request.Body.Filters),
            };
            prm.WithCommonPrm(CommonPrm.FromRequest(request));
            return prm;
        }
    }
}
