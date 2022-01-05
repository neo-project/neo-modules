using Google.Protobuf;
using Neo.FileStorage.Storage.Services.Object.Put;
using System.Linq;
using static Neo.FileStorage.Storage.Helper;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.Tests.Services.Object.Put
{
    public sealed class SimpleObjectTarget : IObjectTarget
    {
        public FSObject Object;
        private ByteString payload = ByteString.Empty;

        public void WriteHeader(FSObject obj)
        {
            Object = obj;
        }

        public void WriteChunk(byte[] chunk)
        {
            payload = ByteString.CopyFrom(payload.Concat(chunk).ToArray());
        }

        public AccessIdentifiers Close()
        {
            Object.Payload = Object.Payload.Concat(payload);
            return new()
            {
                Self = Object.ObjectId,
            };
        }

        public void Dispose() { }
    }
}
