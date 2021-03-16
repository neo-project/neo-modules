using Grpc.Core;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Session;
using UtilSignService = Neo.FileStorage.Services.Util.SignService;

namespace Neo.FileStorage.Services.Netmap
{
    public class SignService
    {
        private UtilSignService signSvc;
        private NetmapService.NetmapServiceBase svc;

        public SignService(byte[] pk, NetmapService.NetmapServiceBase nSvc)
        {
            this.signSvc = new UtilSignService(pk);
            this.svc = nSvc;
        }

        public LocalNodeInfoResponse LocalNodeInfo(ServerCallContext ctx, LocalNodeInfoRequest req)
        {
            var resp = this.signSvc.HandleUnaryRequest(req, (r) =>
            {
                return (IResponse)this.svc.LocalNodeInfo((LocalNodeInfoRequest)r, ctx);
            });
            return (LocalNodeInfoResponse)resp;
        }
    }
}
