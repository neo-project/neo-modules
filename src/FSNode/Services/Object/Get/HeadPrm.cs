using NeoFS.API.v2.Object;
using NeoFS.API.v2.Refs;
using Neo.FSNode.Services.Object.Util;
using Neo.FSNode.Services.Object.Get.Writer;

namespace Neo.FSNode.Services.Object.Get
{
    public class HeadPrm : GetCommonPrm
    {
        public bool Short;

        public static HeadPrm FromRequest(HeadRequest request)
        {
            var prm = new HeadPrm
            {
                Short = request.Body.MainOnly,
                Address = request.Body.Address,
                Raw = request.Body.Raw,
                HeaderWriter = new SimpleObjectWriter(),
            };
            prm.WithCommonPrm(CommonPrm.FromRequest(request));
            return prm;
        }
    }
}