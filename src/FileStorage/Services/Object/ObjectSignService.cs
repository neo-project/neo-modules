
using Neo.FileStorage.API.Object;
using System;

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

        public HeadResponse Head(HeadRequest request)
        {
            return (HeadResponse)HandleUnaryRequest(request, resp =>
            {
                return ResponseService.Head((HeadRequest)resp);
            });
        }

        public IPutRequestStream Put()
        {
            var next = ResponseService.Put();
            return new PutSignStream
            {
                Stream = CreateRequestStreamer(request => next.Send((PutRequest)request), () => next.Close()),
            };
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
