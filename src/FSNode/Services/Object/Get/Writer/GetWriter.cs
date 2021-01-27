using Google.Protobuf;
using Grpc.Core;
using NeoFS.API.v2.Object;
using NeoFS.API.v2.Cryptography;
using V2Object = NeoFS.API.v2.Object.Object;
using System.Security.Cryptography;
using Neo.FSNode.Utils;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Neo.FSNode.Services.Object.Get.Writer
{
    public class GetWriter : IHeaderWriter, IChunkWriter
    {
        private readonly IServerStreamWriter<GetResponse> stream;
        private readonly ECDsa key;

        public GetWriter(IServerStreamWriter<GetResponse> responseStream)
        {
            stream = responseStream;
        }
        public void WriteHeader(V2Object obj)
        {
            var resp = Responser.GetInitResponse(obj);
            resp.SignResponse(key);
            stream.WriteAsync(resp);
        }

        public void WriteChunk(byte[] chunk)
        {
            var resp = Responser.GetChunkResponse(ByteString.CopyFrom(chunk));
            resp.SignResponse(key);
            stream.WriteAsync(resp);
        }
    }
}
