using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Storage.Services.Object.Util;
using System;
using System.Collections.Generic;
using static Neo.FileStorage.Storage.Helper;

namespace Neo.FileStorage.Storage.Services.Object.Search
{
    public class SearchPrm : CommonPrm
    {
        public ContainerID ContainerID;
        public SearchFilters Filters;
        public ISearchResponseWriter Writer;
        public Func<IFSRawClient, List<ObjectID>> Forwarder;
        public static SearchPrm FromRequest(SearchRequest request)
        {
            var cid = request.Body?.ContainerId;
            ContainerIDCheck(cid);
            var prm = new SearchPrm
            {
                ContainerID = cid,
                Filters = new SearchFilters(request.Body.Filters),
            };
            prm.WithCommonPrm(CommonPrm.FromRequest(request));
            return prm;
        }
    }
}
