using Grpc.Core;
using Neo.FileStorage.API.Session;
using UtilSignService = Neo.FileStorage.Services.Util.SignService;

namespace Neo.FileStorage.Services.Session
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
