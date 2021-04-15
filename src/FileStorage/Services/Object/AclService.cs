using Grpc.Core;
using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Object;
using System;
using System.Threading.Tasks;

namespace Neo.FileStorage.Services.Object.Acl
{
    public partial class AclService : ObjectService.ObjectServiceBase
    {
        public ObjectSignService SignService { get; init; }
        public AclChecker AclChecker { get; init; }

        public override Task<DeleteResponse> Delete(DeleteRequest request, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                AclChecker.CheckRequest(request, Operation.Delete);
                return SignService.Delete(request);
            }, context.CancellationToken);
        }

        public override Task Get(GetRequest request, IServerStreamWriter<GetResponse> responseStream, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                var info = AclChecker.CheckRequest(request, Operation.Get);
                SignService.Get(request, resp =>
                {
                    if (resp.Body.ObjectPartCase == GetResponse.Types.Body.ObjectPartOneofCase.Init)
                    {
                        AclChecker.EAclCheck(info, resp);
                    }
                    responseStream.WriteAsync(resp);
                });
            }, context.CancellationToken);
        }

        public override Task GetRange(GetRangeRequest request, IServerStreamWriter<GetRangeResponse> responseStream, ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        public override Task<GetRangeHashResponse> GetRangeHash(GetRangeHashRequest request, ServerCallContext context)
        {
            throw new NotImplementedException();
        }

        public override Task<HeadResponse> Head(HeadRequest request, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                var info = AclChecker.CheckRequest(request, Operation.Head);
                var resp = SignService.Head(request);
                AclChecker.EAclCheck(info, resp);
                return resp;
            }, context.CancellationToken);
        }

        public override async Task<PutResponse> Put(IAsyncStreamReader<PutRequest> requestStream, ServerCallContext context)
        {
            var next = SignService.Put();
            RequestInfo info = null;
            bool init_received = false;
            while (await requestStream.MoveNext())
            {
                var request = requestStream.Current;
                switch (request.Body.ObjectPartCase)
                {
                    case PutRequest.Types.Body.ObjectPartOneofCase.Init:
                        info = AclChecker.CheckRequest(request, Operation.Put);
                        init_received = true;
                        break;
                    case PutRequest.Types.Body.ObjectPartOneofCase.Chunk:
                        if (!init_received) throw new InvalidOperationException($"{nameof(AclService)} {nameof(Put)} missing init");
                        break;
                    default:
                        throw new FormatException($"{nameof(AclService)} {nameof(Put)} invalid put request");
                }
                next.Send(request);
            }
            return next.Close();
        }

        public override Task Search(SearchRequest request, IServerStreamWriter<SearchResponse> responseStream, ServerCallContext context)
        {
            throw new NotImplementedException();
        }

    }
}
