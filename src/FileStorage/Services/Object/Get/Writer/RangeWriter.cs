using Google.Protobuf;
using Grpc.Core;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.Services.Object.Util;
using System.Security.Cryptography;

namespace Neo.FileStorage.Services.Object.Get.Writer
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
