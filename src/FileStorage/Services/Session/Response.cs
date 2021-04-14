using Grpc.Core;
using Neo.FileStorage.Services.Util.Response;
using Neo.FileStorage.API.Session;

namespace Neo.FileStorage.Services.Session
{
    public class ResponseService
    {
        private Service respSvc;
        private SessionService.SessionServiceBase svc;

        public ResponseService(Service rSvc, SessionService.SessionServiceBase nSvc)
        {
            this.respSvc = rSvc;
            this.svc = nSvc;
        }

        public CreateResponse Create(ServerCallContext ctx, CreateRequest req)
        {
            var resp = this.respSvc.HandleUnaryRequest(req, (r) =>
            {
                return (IResponse)this.svc.Create((CreateRequest)r, ctx);
            });
            return (CreateResponse)resp;
        }
    }
}
