using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Storage.Services.Object.Util;
using System.Collections.Generic;
using System.Linq;
using FSRange = Neo.FileStorage.API.Object.Range;
using static Neo.FileStorage.Storage.Helper;

namespace Neo.FileStorage.Storage.Services.Object.Get
{
    public class RangeHashPrm : GetCommonPrm
    {
        public ChecksumType HashType;
        public List<FSRange> Ranges;
        public byte[] Salt;

        public static RangeHashPrm FromRequest(GetRangeHashRequest request)
        {
            var address = request.Body?.Address;
            AddressCheck(address);
            var type = request.Body.Type;
            ChecsumTypeCheck(type);
            var prm = new RangeHashPrm
            {
                Address = address,
                HashType = type,
                Salt = request.Body.Salt.ToByteArray(),
                Ranges = request.Body.Ranges.ToList(),
            };
            prm.WithCommonPrm(CommonPrm.FromRequest(request));
            return prm;
        }
    }
}
