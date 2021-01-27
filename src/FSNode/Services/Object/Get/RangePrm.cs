using NeoFS.API.v2.Object;
using V2Range = NeoFS.API.v2.Object.Range;
using NeoFS.API.v2.Refs;
using Neo.FSNode.Services.Object.Util;
using Neo.FSNode.Services.Object.Get.Writer;

namespace Neo.FSNode.Services.Object.Get
{
    public class RangePrm : GetCommonPrm
    {
        public V2Range Range;

        public static RangePrm FromRequest(GetRangeRequest request)
        {
            var prm = new RangePrm
            {
                Address = request.Body.Address,
                Range = request.Body.Range,
            };
            prm.WithCommonPrm(CommonPrm.FromRequest(request));
            return prm;
        }
    }
}
