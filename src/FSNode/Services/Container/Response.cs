using Grpc.Core;
using Neo.FSNode.Services.Util.Response;
using NeoFS.API.v2.Container;
using NeoFS.API.v2.Session;

namespace Neo.FSNode.Services.Container
{
    public class ResponseService
    {
        private Service respSvc;
        private ContainerService.ContainerServiceBase svc;

        public ResponseService(Service rSvc, ContainerService.ContainerServiceBase cSvc)
        {
            this.respSvc = rSvc;
            this.svc = cSvc;
        }

        public PutResponse Put(ServerCallContext ctx, PutRequest req)
        {
            var resp = this.respSvc.HandleUnaryRequest(req, (r) =>
            {
                return (IResponse)this.svc.Put((PutRequest)r, ctx);
            });
            return (PutResponse)resp;
        }

        public DeleteResponse Delete(ServerCallContext ctx, DeleteRequest req)
        {
            var resp = this.respSvc.HandleUnaryRequest(req, (r) =>
            {
                return (IResponse)this.svc.Delete((DeleteRequest)r, ctx);
            });
            return (DeleteResponse)resp;
        }

        public GetResponse Get(ServerCallContext ctx, GetRequest req)
        {
            var resp = this.respSvc.HandleUnaryRequest(req, (r) =>
            {
                return (IResponse)this.svc.Get((GetRequest)r, ctx);
            });
            return (GetResponse)resp;
        }

        public ListResponse List(ServerCallContext ctx, ListRequest req)
        {
            var resp = this.respSvc.HandleUnaryRequest(req, (r) =>
            {
                return (IResponse)this.svc.List((ListRequest)r, ctx);
            });
            return (ListResponse)resp;
        }

        public GetExtendedACLResponse GetExtendedACL(ServerCallContext ctx, GetExtendedACLRequest req)
        {
            var resp = this.respSvc.HandleUnaryRequest(req, (r) =>
            {
                return (IResponse)this.svc.GetExtendedACL((GetExtendedACLRequest)r, ctx);
            });
            return (GetExtendedACLResponse)resp;
        }

        public SetExtendedACLResponse SetExtendedACL(ServerCallContext ctx, SetExtendedACLRequest req)
        {
            var resp = this.respSvc.HandleUnaryRequest(req, (r) =>
            {
                return (IResponse)this.svc.SetExtendedACL((SetExtendedACLRequest)r, ctx);
            });
            return (SetExtendedACLResponse)resp;
        }
    }
}
