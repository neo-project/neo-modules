using Neo.FileStorage.API.Object;
using System;

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

        public HeadResponse Head(HeadRequest request)
        {
            return (HeadResponse)HandleUnaryRequest(request, r =>
            {
                return SplitService.Head((HeadRequest)r);
            });
        }

        public RequestResponseStream Put()
        {
            var next = SplitService.Put();
            return CreateRequestStream(req => next.Send((PutRequest)req), () => next.Close());
        }
    }

    public class PutResponseStream : IPutRequestStream
    {
        public RequestResponseStream Stream { get; init; }

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
