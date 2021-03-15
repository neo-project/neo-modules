using Google.Protobuf;
using Grpc.Core;
using NeoFS.API.v2.Object;
using NeoFS.API.v2.Cryptography;
using Neo.FSNode.Services.Object.Util;
using System.Security.Cryptography;

namespace Neo.FSNode.Services.Object.Get.Writer
{
    public class RangeWriter : IChunkWriter
    {
        private readonly IServerStreamWriter<GetRangeResponse> stream;
        private readonly Responser responser;

        public RangeWriter(IServerStreamWriter<GetRangeResponse> stream, Responser responser)
        {
            this.stream = stream;
            this.responser = responser;
        }

        public void WriteChunk(byte[] chunk)
        {
            var resp = responser.GetRangeResponse(ByteString.CopyFrom(chunk));
            stream.WriteAsync(resp);
        }
    }
}
