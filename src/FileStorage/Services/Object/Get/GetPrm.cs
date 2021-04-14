using Neo.FileStorage.API.Object;
using Neo.FileStorage.Services.Object.Util;

namespace Neo.FileStorage.Services.Object.Get
{
    public class GetPrm : GetCommonPrm
    {
        public static GetPrm FromRequest(GetRequest request)
        {
            var prm = new GetPrm
            {
                Address = request.Body.Address,
                Raw = request.Body.Raw,
            };
            prm.WithCommonPrm(CommonPrm.FromRequest(request));
            return prm;
        }
    }
}
