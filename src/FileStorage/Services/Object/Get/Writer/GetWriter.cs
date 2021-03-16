using Google.Protobuf;
using Grpc.Core;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.Services.Object.Util;
using System.Security.Cryptography;
using V2Object = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Services.Object.Get.Writer
{
    public class GetWriter : IHeaderWriter, IChunkWriter
    {
        private readonly IServerStreamWriter<GetResponse> stream;
        private readonly Responser responser;

        public GetWriter(IServerStreamWriter<GetResponse> stream, Responser responser)
        {
            this.stream = stream;
            this.responser = responser;
        }
        public void WriteHeader(V2Object obj)
        {
            var resp = responser.GetInitResponse(obj);
            stream.WriteAsync(resp);
        }

        public void WriteChunk(byte[] chunk)
        {
            var resp = responser.GetChunkResponse(ByteString.CopyFrom(chunk));
            stream.WriteAsync(resp);
        }
    }
}
