using Neo.FileStorage.API.Object;
using Neo.FileStorage.Storage.Services.Object.Util;
using static Neo.FileStorage.Storage.Helper;

namespace Neo.FileStorage.Storage.Services.Object.Get
{
    public class HeadPrm : GetCommonPrm
    {
        public bool Short;

        public static HeadPrm FromRequest(HeadRequest request)
        {
            var address = request.Body?.Address;
            AddressCheck(address);
            var prm = new HeadPrm
            {
                Short = request.Body.MainOnly,
                Address = request.Body.Address,
                Raw = request.Body.Raw,
            };
            prm.WithCommonPrm(CommonPrm.FromRequest(request));
            return prm;
        }
    }
}
