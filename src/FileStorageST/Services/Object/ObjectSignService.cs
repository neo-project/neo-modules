using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Status;
using System;
using System.Threading;
using static Neo.FileStorage.Storage.Services.Util.Helper;

namespace Neo.FileStorage.Storage.Services.Object
{
    public class ObjectSignService : SignService
    {
        public ObjectResponseService ResponseService { get; init; }

        public DeleteResponse Delete(DeleteRequest request, CancellationToken cancellation)
        {
            return (DeleteResponse)HandleUnaryRequest(request, resp =>
            {
                return ResponseService.Delete((DeleteRequest)resp, cancellation);
            }, () => new DeleteResponse());
        }

        public void Get(GetRequest request, Action<GetResponse> handler, CancellationToken cancellation)
        {
            try
            {
                ResponseService.Get(request, HandleServerStreamRequest(request, resp =>
                {
                    handler((GetResponse)resp);
                }), cancellation);
            }
            catch (Exception e)
            {
                if (!IsStatusSupported(request)) throw;
                Utility.Log(nameof(ObjectSignService.Get), LogLevel.Debug, e.Message);
                var resp = new GetResponse();
                resp.SetStatus(e);
                Key.Sign(resp);
                handler(resp);
            }
        }

        public void GetRange(GetRangeRequest request, Action<GetRangeResponse> handler, CancellationToken cancellation)
        {
            try
            {
                ResponseService.GetRange(request, HandleServerStreamRequest(request, resp =>
                {
                    handler((GetRangeResponse)resp);
                }), cancellation);
            }
            catch (Exception e)
            {
                if (!IsStatusSupported(request)) throw;
                Utility.Log(nameof(ObjectSignService.GetRange), LogLevel.Debug, e.Message);
                var resp = new GetRangeResponse();
                resp.SetStatus(e);
                Key.Sign(resp);
                handler(resp);
            }
        }

        public GetRangeHashResponse GetRangeHash(GetRangeHashRequest request, CancellationToken cancellation)
        {
            return (GetRangeHashResponse)HandleUnaryRequest(request, resp =>
            {
                return ResponseService.GetRangeHash((GetRangeHashRequest)resp, cancellation);
            }, () => new GetRangeHashResponse());
        }

        public HeadResponse Head(HeadRequest request, CancellationToken cancellation)
        {
            return (HeadResponse)HandleUnaryRequest(request, resp =>
            {
                return ResponseService.Head((HeadRequest)resp, cancellation);
            }, () => new HeadResponse());
        }

        public IRequestStream Put(CancellationToken cancellation)
        {
            var next = ResponseService.Put(cancellation);
            return new PutSignStream
            {
                Stream = CreateRequestStreamer(request => next.Send((PutRequest)request), () => next.Close(), () => new PutResponse(), () => next.Dispose()),
            };
        }

        public void Search(SearchRequest request, Action<SearchResponse> handler, CancellationToken cancellation)
        {
            ResponseService.Search(request, HandleServerStreamRequest(request, resp =>
            {
                handler((SearchResponse)resp);
            }), cancellation);
        }
    }
}
