using Neo.FileStorage.API.Object;
using System;
using System.Threading;
using Neo.FileStorage.Storage.Services.Object.Get.Writer;

namespace Neo.FileStorage.Storage.Services.Object.Get
{
    public partial class GetService
    {
        public GetPrm ToGetPrm(GetRequest request, Action<GetResponse> handler, CancellationToken cancellation)
        {
            var prm = GetPrm.FromRequest(request);
            prm.Writer = new GetStream
            {
                Handler = handler
            };
            if (!prm.Local)
                prm.Forwarder = new(KeyStore, request, cancellation);
            return prm;
        }

        public HeadPrm ToHeadPrm(HeadRequest request, HeadResponse response, CancellationToken cancellation)
        {
            var prm = HeadPrm.FromRequest(request);
            prm.Writer = new HeadResponseWriter
            {
                Short = request.Body.MainOnly,
                Response = response,
            };
            if (!prm.Local)
                prm.Forwarder = new(KeyStore, request, cancellation);
            return prm;
        }

        public RangeHashPrm ToRangeHashPrm(GetRangeHashRequest request, CancellationToken cancellation)
        {
            var prm = RangeHashPrm.FromRequest(request);
            if (!prm.Local)
                prm.Key = KeyStore.GetKey(null);
            return prm;
        }

        public RangePrm ToRangePrm(GetRangeRequest request, Action<GetRangeResponse> handler, CancellationToken cancellation)
        {
            var prm = RangePrm.FromRequest(request);
            prm.Writer = new RangeStream
            {
                Handler = handler,
            };
            if (!prm.Local)
                prm.Forwarder = new(KeyStore, request, cancellation);
            return prm;
        }
    }
}
