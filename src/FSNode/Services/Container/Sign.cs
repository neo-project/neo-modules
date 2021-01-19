using Grpc.Core;
using NeoFS.API.v2.Container;
using NeoFS.API.v2.Session;
using UtilSignService = Neo.FSNode.Services.Util.SignService;

namespace Neo.FSNode.Services.Container
{
    public class SignService
    {
        private UtilSignService signSvc;
        private ContainerService.ContainerServiceBase svc;

        public SignService(byte[] pk, ContainerService.ContainerServiceBase cSvc)
        {
            this.signSvc = new UtilSignService(pk);
            this.svc = cSvc;
        }

        public PutResponse Put(ServerCallContext ctx, PutRequest req)
        {
            var resp = this.signSvc.HandleUnaryRequest(req, (r) =>
            {
                return (IResponse)this.svc.Put((PutRequest)r, ctx);
            });
            return (PutResponse)resp;
        }

        public DeleteResponse Delete(ServerCallContext ctx, DeleteRequest req)
        {
            var resp = this.signSvc.HandleUnaryRequest(req, (r) =>
            {
                return (IResponse)this.svc.Delete((DeleteRequest)r, ctx);
            });
            return (DeleteResponse)resp;
        }

        public GetResponse Get(ServerCallContext ctx, GetRequest req)
        {
            var resp = this.signSvc.HandleUnaryRequest(req, (r) =>
            {
                return (IResponse)this.svc.Get((GetRequest)r, ctx);
            });
            return (GetResponse)resp;
        }

        public ListResponse List(ServerCallContext ctx, ListRequest req)
        {
            var resp = this.signSvc.HandleUnaryRequest(req, (r) =>
            {
                return (IResponse)this.svc.List((ListRequest)r, ctx);
            });
            return (ListResponse)resp;
        }

        public GetExtendedACLResponse GetExtendedACL(ServerCallContext ctx, GetExtendedACLRequest req)
        {
            var resp = this.signSvc.HandleUnaryRequest(req, (r) =>
            {
                return (IResponse)this.svc.GetExtendedACL((GetExtendedACLRequest)r, ctx);
            });
            return (GetExtendedACLResponse)resp;
        }

        public SetExtendedACLResponse SetExtendedACL(ServerCallContext ctx, SetExtendedACLRequest req)
        {
            var resp = this.signSvc.HandleUnaryRequest(req, (r) =>
            {
                return (IResponse)this.svc.SetExtendedACL((SetExtendedACLRequest)r, ctx);
            });
            return (SetExtendedACLResponse)resp;
        }
    }
}
