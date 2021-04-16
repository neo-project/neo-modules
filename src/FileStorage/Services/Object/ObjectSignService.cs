
using Neo.FileStorage.API.Object;
using System;
using System.Threading;

namespace Neo.FileStorage.Services.Object
{
    public class ObjectSignService : SignService
    {
        public ObjectResponseService ResponseService { get; init; }

        public DeleteResponse Delete(DeleteRequest request)
        {
            return (DeleteResponse)HandleUnaryRequest(request, resp =>
            {
                return ResponseService.Delete((DeleteRequest)resp);
            });
        }

        public void Get(GetRequest request, Action<GetResponse> handler)
        {
            ResponseService.Get(request, resp => HandleServerStreamRequest(request, resp =>
            {
                handler((GetResponse)resp);
            }));
        }

        public void GetRange(GetRangeRequest request, Action<GetRangeResponse> handler)
        {
            ResponseService.GetRange(request, resp => HandleServerStreamRequest(request, resp =>
            {
                handler((GetRangeResponse)resp);
            }));
        }

        public GetRangeHashResponse GetRangeHash(GetRangeHashRequest request)
        {
            return (GetRangeHashResponse)HandleUnaryRequest(request, resp =>
            {
                return ResponseService.GetRangeHash((GetRangeHashRequest)resp);
            });
        }

        public HeadResponse Head(HeadRequest request)
        {
            return (HeadResponse)HandleUnaryRequest(request, resp =>
            {
                return ResponseService.Head((HeadRequest)resp);
            });
        }

        public IPutRequestStream Put(CancellationToken cancellation)
        {
            var next = ResponseService.Put(cancellation);
            return new PutSignStream
            {
                Stream = CreateRequestStreamer(request => next.Send((PutRequest)request), () => next.Close()),
            };
        }

        public void Search(SearchRequest request, Action<SearchResponse> handler)
        {
            ResponseService.Search(request, resp => HandleServerStreamRequest(request, resp =>
            {
                handler((SearchResponse)resp);
            }));
        }
    }

    public class PutSignStream : IPutRequestStream
    {
        public RequestSignStream Stream { get; init; }

        public void Send(PutRequest request)
        {
            Stream.Send(request);
        }

        public PutResponse Close()
        {
            return (PutResponse)Stream.Close();
        }
    }
}
