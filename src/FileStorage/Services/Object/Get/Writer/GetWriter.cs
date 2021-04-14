using Google.Protobuf;
using Grpc.Core;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.Services.Object.Util;
using Neo.FileStorage.Services.Object.Acl;
using System;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Services.Object.Get.Writer
{
    public class GetWriter : IObjectWriter
    {
        public IServerStreamWriter<GetResponse> Stream { get; init; }
        public Responser Responser { get; init; }
        public AclChecker AclChecker { get; init; }
        public RequestInfo Info { get; init; }

        public void WriteHeader(FSObject obj)
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
