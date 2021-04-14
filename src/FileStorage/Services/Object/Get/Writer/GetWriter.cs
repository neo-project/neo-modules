using Google.Protobuf;
using Grpc.Core;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.Services.Object.Util;
using System;
using V2Object = Neo.FileStorage.API.Object.Object;
using Neo.FileStorage.Services.Object.Acl;

namespace Neo.FileStorage.Services.Object.Get.Writer
{
    public class GetWriter : IHeaderWriter, IChunkWriter
    {
        public IServerStreamWriter<GetResponse> Stream { get; init; }
        public Responser Responser { get; init; }
        public AclChecker AclChecker { get; init; }
        public RequestInfo Info { get; init; }

        public void WriteHeader(V2Object obj)
        {
            var resp = Responser.GetInitResponse(obj);
            if (!AclChecker.EAclCheck(Info, resp)) throw new Exception(nameof(WriteHeader) + " eacl check failed");
            Stream.WriteAsync(resp);
        }

        public void WriteChunk(byte[] chunk)
        {
            var resp = Responser.GetChunkResponse(ByteString.CopyFrom(chunk));
            Stream.WriteAsync(resp);
        }
    }
}
