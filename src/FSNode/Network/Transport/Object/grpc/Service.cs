using Grpc.Core;
using NeoFS.API.v2.Object;

namespace Neo.FSNode.Network.Transport.Object
{
    public class Service
    {
        private ObjectService.ObjectServiceBase srv;

        public Service(ObjectService.ObjectServiceBase service)
        {
            this.srv = service;
        }

        public void Get(GetRequest req, IServerStreamWriter<GetResponse> gStream, ServerCallContext ctx)
        {
            // TODO
            var t = this.srv.Get(req, gStream, ctx);
            t.Wait();
        }

        public void Put(IAsyncStreamReader<PutRequest> gStream, ServerCallContext ctx)
        {
            // TODO
            this.srv.Put(gStream, ctx).Wait();
        }

        public DeleteResponse Delete(DeleteRequest req, ServerCallContext ctx)
        {
            return this.Delete(req, ctx);
        }

        public HeadResponse Head(HeadRequest req, ServerCallContext ctx)
        {
            return this.Head(req, ctx);
        }

        public void Search(SearchRequest req, IServerStreamWriter<SearchResponse> gStream, ServerCallContext ctx)
        {
            // TODO
            this.srv.Search(req, gStream, ctx).Wait();
        }

        public void GetRange(GetRangeRequest req, IServerStreamWriter<GetRangeResponse> gStream, ServerCallContext ctx)
        {
            // TODO
            this.srv.GetRange(req, gStream, ctx).Wait();
        }

        public GetRangeHashResponse GetRangeHash(GetRangeHashRequest req, ServerCallContext ctx)
        {
            return this.srv.GetRangeHash(req, ctx).Result;
        }
    }
}
