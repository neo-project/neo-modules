using Grpc.Core;
using Neo.FileStorage.Services.Util.Response;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Session;

namespace Neo.FileStorage.Services.Netmap
{
    public class ResponseService
    {
        private Service respSvc;
        private NetmapService.NetmapServiceBase svc;

        public ResponseService(Service rSvc, NetmapService.NetmapServiceBase nSvc)
        {
            this.respSvc = rSvc;
            this.svc = nSvc;
        }

        public LocalNodeInfoResponse LocalNodeInfo(ServerCallContext ctx, LocalNodeInfoRequest req)
        {
            var resp = this.respSvc.HandleUnaryRequest(req, (r) =>
            {
                return (IResponse)this.svc.LocalNodeInfo((LocalNodeInfoRequest)r, ctx);
            });
            return (LocalNodeInfoResponse)resp;
        }
    }
}
