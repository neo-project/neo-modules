using System;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.Services.Object.Get.Writer;
using Neo.FileStorage.Services.Object.Util;

namespace Neo.FileStorage.Services.Object.Get
{
    public partial class GetService
    {
        public GetPrm ToGetPrm(GetRequest request, Action<GetResponse> handler)
        {
            var key = KeyStorage.GetKey(request.MetaHeader.SessionToken);
            var prm = GetPrm.FromRequest(request);
            prm.Key = key;
            prm.Writer = new GetResponseWriter
            {
                Handler = handler,
            };
            return prm;
        }

        public HeadPrm ToHeadPrm(HeadRequest request, HeadResponse response)
        {
            var key = KeyStorage.GetKey(request.MetaHeader.SessionToken);
            var prm = HeadPrm.FromRequest(request);
            prm.Key = key;
            prm.Writer = new HeadResponseWriter
            {
                Short = request.Body.MainOnly,
                Response = response,
            };
            return prm;
        }

        public RangeHashPrm ToRangeHashPrm(GetRangeHashRequest request, GetRangeHashResponse resp)
        {
            return new();
        }

        public RangePrm ToRangePrm(GetRangeRequest request, Action<GetRangeResponse> handler)
        {
            return new();
        }
    }
}
