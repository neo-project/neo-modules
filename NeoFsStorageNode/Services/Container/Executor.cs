using Grpc.Core;
using NeoFS.API.v2.Container;

namespace Neo.Fs.Services.Container
{
    public interface IServiceExecutor
    {
        PutResponse.Types.Body Put(ServerCallContext ctx, PutRequest.Types.Body body);
        DeleteResponse.Types.Body Delete(ServerCallContext ctx, DeleteRequest.Types.Body body);
        GetResponse.Types.Body Get(ServerCallContext ctx, GetRequest.Types.Body body);
        ListResponse.Types.Body List(ServerCallContext ctx, ListRequest.Types.Body body);
        SetExtendedACLResponse.Types.Body SetExtendedACL(ServerCallContext ctx, SetExtendedACLRequest.Types.Body body);
        GetExtendedACLResponse.Types.Body GetExtendedACL(ServerCallContext ctx, GetExtendedACLRequest.Types.Body body);
    }

    public class ExecutorSvc
    {
        private IServiceExecutor exec;

        public ExecutorSvc(IServiceExecutor ise)
        {
            this.exec = ise;
        }

        public PutResponse Put(ServerCallContext ctx, PutRequest req)
        {
            var body = this.exec.Put(ctx, req.Body);
            return new PutResponse() { Body = body };
        }

        public DeleteResponse Delete(ServerCallContext ctx, DeleteRequest req)
        {
            var body = this.exec.Delete(ctx, req.Body);
            return new DeleteResponse() { Body = body };
        }

        public GetResponse Get(ServerCallContext ctx, GetRequest req)
        {
            var body = this.exec.Get(ctx, req.Body);
            return new GetResponse() { Body = body };
        }

        public ListResponse List(ServerCallContext ctx, ListRequest req)
        {
            var body = this.exec.List(ctx, req.Body);
            return new ListResponse() { Body = body };
        }

        public GetExtendedACLResponse GetExtendedACL(ServerCallContext ctx, GetExtendedACLRequest req)
        {
            var body = this.exec.GetExtendedACL(ctx, req.Body);
            return new GetExtendedACLResponse() { Body = body };
        }

        public SetExtendedACLResponse SetExtendedACL(ServerCallContext ctx, SetExtendedACLRequest req)
        {
            var body = this.exec.SetExtendedACL(ctx, req.Body);
            return new SetExtendedACLResponse() { Body = body };
        }
    }
}
