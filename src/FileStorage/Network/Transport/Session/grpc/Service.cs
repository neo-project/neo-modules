using Grpc.Core;
using Neo.FileStorage.API.Session;

namespace Neo.FileStorage.Network.Transport.Session.grpc
{
    public class Service
    {
        private SessionService.SessionServiceBase srv;

        public Service(SessionService.SessionServiceBase service)
        {
            this.srv = service;
        }

        public CreateResponse Create(CreateRequest req, ServerCallContext ctx)
        {
            return this.srv.Create(req, ctx).Result;
        }
    }
}
