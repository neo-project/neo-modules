using Google.Protobuf;
using Grpc.Core;
using NeoFS.API.v2.Object;
using NeoFS.API.v2.Cryptography;
using System.Security.Cryptography;

namespace Neo.FSNode.Services.Object.Get.Writer
{
    public class RangeWriter : IChunkWriter
    {
        private readonly IServerStreamWriter<GetRangeResponse> stream;
        private readonly ECDsa key;

        public RangeWriter(IServerStreamWriter<GetRangeResponse> stream, ECDsa key)
        {
            this.key = key;
            this.stream = stream;
        }

        public void WriteChunk(byte[] chunk)
        {
            var resp = Responser.GetRangeResponse(ByteString.CopyFrom(chunk));
            resp.SignResponse(key);
            stream.WriteAsync(resp);
        }
    }
}
