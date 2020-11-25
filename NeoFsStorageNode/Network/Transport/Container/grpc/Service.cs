using Grpc.Core;
using NeoFS.API.v2.Container;

namespace Neo.Fs.Network.Transport.Container
{
    public class Service
    {
        private ContainerService.ContainerServiceBase srv;

        public Service(ContainerService.ContainerServiceBase service)
        {
            this.srv = service;
        }

        public PutResponse Put(ServerCallContext ctx, PutRequest req)
        {
            return this.srv.Put(req, ctx).Result;
        }

        public GetResponse Get(ServerCallContext ctx, GetRequest req)
        {
            return this.srv.Get(req, ctx).Result;
        }

        public DeleteResponse Delete(ServerCallContext ctx, DeleteRequest req)
        {
            return this.srv.Delete(req, ctx).Result;
        }

        public ListResponse List(ServerCallContext ctx, ListRequest req)
        {
            return this.srv.List(req, ctx).Result;
        }

        public SetExtendedACLResponse SetExtendedACL(ServerCallContext ctx, SetExtendedACLRequest req)
        {
            return this.srv.SetExtendedACL(req, ctx).Result;
        }

        public GetExtendedACLResponse GetExtendedACL(ServerCallContext ctx, GetExtendedACLRequest req)
        {
            return this.srv.GetExtendedACL(req, ctx).Result;
        }
    }
}
