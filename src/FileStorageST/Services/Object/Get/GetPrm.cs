using Neo.FileStorage.API.Object;
using Neo.FileStorage.Storage.Services.Object.Util;
using static Neo.FileStorage.Storage.Helper;

namespace Neo.FileStorage.Storage.Services.Object.Get
{
    public class GetPrm : GetCommonPrm
    {
        public static GetPrm FromRequest(GetRequest request)
        {
            var address = request.Body?.Address;
            AddressCheck(address);
            var prm = new GetPrm
            {
                Address = address,
                Raw = request.Body.Raw,
            };
            prm.WithCommonPrm(CommonPrm.FromRequest(request));
            return prm;
        }
    }
}
