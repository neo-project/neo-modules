using Google.Protobuf;
using Grpc.Core;
using NeoFS.API.v2.Object;
using NeoFS.API.v2.Cryptography;
using Neo.FSNode.Services.Object.Util;
using System.Security.Cryptography;
using V2Object = NeoFS.API.v2.Object.Object;

namespace Neo.FSNode.Services.Object.Get.Writer
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
