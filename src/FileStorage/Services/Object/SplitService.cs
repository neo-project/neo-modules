using Google.Protobuf;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.Services.Object.Put;
using System;
using System.Linq;
using System.Threading;

namespace Neo.FileStorage.Services.Object
{
    public class SplitService
    {
        public int ChunkSize { get; init; }
        public int AddressAmount { get; init; }
        public ObjectService ObjectService { get; init; }

        public DeleteResponse Delete(DeleteRequest request)
        {
            return ObjectService.Delete(request);
        }

        public void Get(GetRequest request, Action<GetResponse> handler)
        {
            ObjectService.Get(request, resp =>
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

        public void GetRange(GetRangeRequest request, Action<GetRangeResponse> handler)
        {
            ObjectService.GetRange(request, resp =>
            {
                switch (resp.Body.RangePartCase)
                {
                    case GetRangeResponse.Types.Body.RangePartOneofCase.None:
                    case GetRangeResponse.Types.Body.RangePartOneofCase.SplitInfo:
                        handler(resp);
                        break;
                    case GetRangeResponse.Types.Body.RangePartOneofCase.Chunk:
                        var buffer = resp.Body.Chunk.ToByteArray().AsEnumerable();
                        while (buffer.Any())
                        {
                            var chunk = buffer.Take(ChunkSize);
                            resp.Body.Chunk = ByteString.CopyFrom(chunk.ToArray());
                            buffer = buffer.Skip(chunk.Count());
                            handler(resp);
                        }
                        break;
                    default:
                        throw new InvalidOperationException($"{nameof(SplitService)} invalid {resp.GetType()}");
                }
            });
        }

        public GetRangeHashResponse GetRangeHash(GetRangeHashRequest request)
        {
            return ObjectService.GetRangeHash(request);
        }

        public HeadResponse Head(HeadRequest request)
        {
            return ObjectService.Head(request);
        }

        public PutStream Put(CancellationToken cancellation)
        {
            return ObjectService.Put(cancellation);
        }

        public void Search(SearchRequest request, Action<SearchResponse> handler)
        {
            ObjectService.Search(request, resp =>
            {
                var ids = resp.Body.IdList.AsEnumerable();
                while (ids.Any())
                {
                    SearchResponse r = new();
                    r.MetaHeader = resp.MetaHeader;
                    r.VerifyHeader = resp.VerifyHeader;
                    r.Body = new SearchResponse.Types.Body();
                    r.Body.IdList.AddRange(ids.Take(AddressAmount));
                    ids = ids.Skip(r.Body.IdList.Count);
                    handler(r);
                }
            });
        }
    }
}
