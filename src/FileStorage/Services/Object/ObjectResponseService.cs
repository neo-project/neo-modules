using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Session;
using System;
using System.Threading;

namespace Neo.FileStorage.Services.Object
{
    public class ObjectResponseService : ResponseService
    {
        public SplitService SplitService { get; init; }

        public DeleteResponse Delete(DeleteRequest request)
        {
            return (DeleteResponse)HandleUnaryRequest(request, r =>
            {
                return SplitService.Delete((DeleteRequest)r);
            });
        }

        public void Get(GetRequest request, Action<GetResponse> handler)
        {
            SplitService.Get(request, resp => HandleServerStreamRequest(resp =>
            {
                handler((GetResponse)resp);
            }));
        }

        public void GetRange(GetRangeRequest request, Action<GetRangeResponse> handler)
        {
            SplitService.GetRange(request, resp => HandleServerStreamRequest(resp =>
            {
                handler((GetRangeResponse)resp);
            }));
        }

        public GetRangeHashResponse GetRangeHash(GetRangeHashRequest request)
        {
            return (GetRangeHashResponse)HandleUnaryRequest(request, r =>
            {
                return SplitService.GetRangeHash((GetRangeHashRequest)r);
            });
        }

        public HeadResponse Head(HeadRequest request)
        {
            return (HeadResponse)HandleUnaryRequest(request, r =>
            {
                return SplitService.Head((HeadRequest)r);
            });
        }

        public IRequestStream Put(CancellationToken cancellation)
        {
            var next = SplitService.Put(cancellation);
            return CreateRequestStream(req => next.Send((PutRequest)req), () => next.Close());
        }

        public void Search(SearchRequest request, Action<SearchResponse> handler)
        {
            SplitService.Search(request, resp => HandleServerStreamRequest(resp =>
            {
                handler((SearchResponse)resp);
            }));
        }
    }

    public class PutResponseStream : IRequestStream
    {
        public RequestResponseStream Stream { get; init; }

        public void Send(IRequest request)
        {
            Stream.Send(request);
        }

        public IResponse Close()
        {
            return (PutResponse)Stream.Close();
        }
    }
}
