using Neo.FileStorage.API.Object;
using System;
using System.Threading;

namespace Neo.FileStorage.Storage.Services.Object
{
    public class ObjectResponseService : ResponseService
    {
        public SplitService SplitService { get; init; }

        public DeleteResponse Delete(DeleteRequest request, CancellationToken cancellation)
        {
            return (DeleteResponse)HandleUnaryRequest(request, r =>
            {
                return SplitService.Delete((DeleteRequest)r, cancellation);
            });
        }

        public void Get(GetRequest request, Action<GetResponse> handler, CancellationToken cancellation)
        {
            SplitService.Get(request, HandleServerStreamRequest(resp =>
            {
                handler((GetResponse)resp);
            }), cancellation);
        }

        public void GetRange(GetRangeRequest request, Action<GetRangeResponse> handler, CancellationToken cancellation)
        {
            SplitService.GetRange(request, HandleServerStreamRequest(resp =>
            {
                handler((GetRangeResponse)resp);
            }), cancellation);
        }

        public GetRangeHashResponse GetRangeHash(GetRangeHashRequest request, CancellationToken cancellation)
        {
            return (GetRangeHashResponse)HandleUnaryRequest(request, r =>
            {
                return SplitService.GetRangeHash((GetRangeHashRequest)r, cancellation);
            });
        }

        public HeadResponse Head(HeadRequest request, CancellationToken cancellation)
        {
            return (HeadResponse)HandleUnaryRequest(request, r =>
            {
                return SplitService.Head((HeadRequest)r, cancellation);
            });
        }

        public IRequestStream Put(CancellationToken cancellation)
        {
            var next = SplitService.Put(cancellation);
            return CreateRequestStream(req => next.Send((PutRequest)req), () => next.Close(), () => next.Dispose());
        }

        public void Search(SearchRequest request, Action<SearchResponse> handler, CancellationToken cancellation)
        {
            SplitService.Search(request, HandleServerStreamRequest(resp =>
            {
                handler((SearchResponse)resp);
            }), cancellation);
        }
    }
}
