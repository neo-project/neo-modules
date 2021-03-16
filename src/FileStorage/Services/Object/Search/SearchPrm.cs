using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Services.Object.Util;

namespace Neo.FileStorage.Services.Object.Search
{
    public class SearchPrm : CommonPrm
    {
        public ContainerID CID;
        public SearchFilters Filters;

        public static SearchPrm FromRequest(SearchRequest request)
        {
            var prm = new SearchPrm
            {
                CID = request.Body.ContainerId,
                Filters = new SearchFilters(request.Body.Filters),
            };
            prm.WithCommonPrm(CommonPrm.FromRequest(request));
            return prm;
        }
    }
}
