using Neo.FileStorage.API.Object;
using Neo.FileStorage.Storage.Services.Object.Util;
using FSRange = Neo.FileStorage.API.Object.Range;
using static Neo.FileStorage.Storage.Helper;

namespace Neo.FileStorage.Storage.Services.Object.Get
{
    public class RangePrm : GetCommonPrm
    {
        public FSRange Range;

        public static RangePrm FromRequest(GetRangeRequest request)
        {
            var address = request.Body?.Address;
            AddressCheck(address);
            var range = request.Body.Range;
            RangeCheck(range);
            var prm = new RangePrm
            {
                Address = address,
                Range = range,
            };
            prm.WithCommonPrm(CommonPrm.FromRequest(request));
            return prm;
        }
    }
}
