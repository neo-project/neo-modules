using NeoFS.API.v2.Object;
using V2Range = NeoFS.API.v2.Object.Range;
using NeoFS.API.v2.Refs;
using Neo.FSNode.Services.Object.Util;

namespace Neo.FSNode.Services.Object.Range
{
    public class RangePrm : CommonPrm
    {
        public bool Full;
        public Address Address;
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
