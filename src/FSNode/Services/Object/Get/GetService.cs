using NeoFS.API.v2.Object;
using V2Object = NeoFS.API.v2.Object.Object;
using Neo.FSNode.Services.Object.Range;

namespace Neo.FSNode.Services.Object.Get
{
    public class GetService
    {
        private RangeService rangeService;

        public V2Object Get(GetPrm prm)
        {
            var obj = new V2Object();
            var range_prm = new RangePrm
            {
                Address = prm.Address,
                Full = true,
            };
            var result = rangeService.Range(range_prm);
            obj.Header = result.Header.Header;
            obj.Payload = result.Chunk;
            return obj;
        }
    }
}
