using Grpc.Core;
using NeoFS.API.v2.Session;

namespace Neo.Fs.Network.Transport.Session.grpc
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
