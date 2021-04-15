using Grpc.Core;
using Google.Protobuf;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.Services.Object.Util;
using System;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Services.Object.Get.Writer
{
    public class RangeWriter : IObjectResponseWriter
    {
        private readonly IServerStreamWriter<GetRangeResponse> stream;
        private readonly Responser responser;

        public RangeWriter(IServerStreamWriter<GetRangeResponse> stream, Responser responser)
        {
            this.stream = stream;
            this.responser = responser;
        }

        public void WriteHeader(FSObject obj)
        {
            throw new NotImplementedException(nameof(WriteHeader));
        }

        public void WriteChunk(byte[] chunk)
        {
            var resp = responser.GetRangeResponse(ByteString.CopyFrom(chunk));
            stream.WriteAsync(resp);
        }
    }
}
