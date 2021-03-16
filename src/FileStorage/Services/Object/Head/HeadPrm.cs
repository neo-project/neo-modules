using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Services.Object.Util;

namespace Neo.FileStorage.Services.Object.Head
{
    public class HeadPrm : CommonPrm
    {
        public Address Address;
        public bool Short;
        public bool Raw;

        public static HeadPrm FromRequest(HeadRequest request)
        {
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