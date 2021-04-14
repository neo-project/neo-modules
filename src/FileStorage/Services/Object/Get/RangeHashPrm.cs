using Neo.FileStorage.API.Object;
using V2Range = Neo.FileStorage.API.Object.Range;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Services.Object.Util;
using System.Collections.Generic;
using System.Linq;

namespace Neo.FileStorage.Services.Object.Get
{
    public class RangeHashPrm : GetCommonPrm
    {
        public ChecksumType HashType;
        public List<V2Range> Ranges;

        public static RangeHashPrm FromRequest(GetRangeHashRequest request)
        {
            var prm = new RangeHashPrm
            {
                Address = request.Body.Address,
                HashType = request.Body.Type,
                Ranges = request.Body.Ranges.ToList(),
            };
            prm.WithCommonPrm(CommonPrm.FromRequest(request));
            return prm;
        }
    }
}
