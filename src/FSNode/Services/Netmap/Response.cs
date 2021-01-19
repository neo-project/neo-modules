using Grpc.Core;
using Neo.FSNode.Services.Util.Response;
using NeoFS.API.v2.Netmap;
using NeoFS.API.v2.Session;

namespace Neo.FSNode.Services.Netmap
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
