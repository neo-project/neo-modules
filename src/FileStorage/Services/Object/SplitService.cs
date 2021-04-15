using Google.Protobuf;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.Services.Object.Put;
using System;
using System.Linq;

namespace Neo.FileStorage.Services.Object
{
    public class SplitService
    {
        public int ChunkSize { get; init; }
        public ObjectServices ObjectServices { get; init; }

        public void Get(GetRequest request, Action<GetResponse> handler)
        {
            ObjectServices.Get(request, resp =>
            {
                switch (resp.Body.ObjectPartCase)
                {
                    case GetResponse.Types.Body.ObjectPartOneofCase.Init:
                    case GetResponse.Types.Body.ObjectPartOneofCase.SplitInfo:
                        handler(resp);
                        break;
                    case GetResponse.Types.Body.ObjectPartOneofCase.Chunk:
                        var buffer = resp.Body.Chunk.ToByteArray().AsEnumerable();
                        while (buffer.Any())
                        {
                            var chunk = buffer.Take(ChunkSize);
                            resp.Body.Chunk = ByteString.CopyFrom(chunk.ToArray());
                            handler(resp);
                            buffer = buffer.Skip(chunk.Count());
                        }
                        break;
                    default:
                        throw new InvalidOperationException($"{nameof(SplitService)} invalid {resp.GetType()}");
                }
            });
        }

        public HeadResponse Head(HeadRequest request)
        {
            return ObjectServices.Head(request);
        }

        public PutStream Put()
        {
            return ObjectServices.Put();
        }
    }
}
