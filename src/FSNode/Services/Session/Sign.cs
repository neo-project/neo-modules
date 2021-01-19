using Grpc.Core;
using NeoFS.API.v2.Session;
using UtilSignService = Neo.FSNode.Services.Util.SignService;

namespace Neo.FSNode.Services.Session
{
    public class SignService
    {
        private UtilSignService signSvc;
        private SessionService.SessionServiceBase svc;

        public SignService(byte[] pk, SessionService.SessionServiceBase sSvc)
        {
            this.signSvc = new UtilSignService(pk);
            this.svc = sSvc;
        }

        public CreateResponse Create(ServerCallContext ctx, CreateRequest req)
        {
            var resp = this.signSvc.HandleUnaryRequest(req, (r) =>
            {
                return (IResponse)this.svc.Create((CreateRequest)r, ctx);
            });
            return (CreateResponse)resp;
        }
    }
}
