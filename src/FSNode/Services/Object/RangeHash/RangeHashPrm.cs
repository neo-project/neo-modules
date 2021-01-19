using NeoFS.API.v2.Object;
using V2Range = NeoFS.API.v2.Object.Range;
using NeoFS.API.v2.Refs;
using Neo.FSNode.Services.Object.Util;
using System.Collections.Generic;
using System.Linq;

namespace Neo.FSNode.Services.Object.RangeHash
{
    public class RangeHashPrm : CommonPrm
    {
        public Address Address;
        public ChecksumType HashType;
        public List<V2Range> Ranges;
        public byte[] Salt;

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
