using Google.Protobuf;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.Services.Object.Put.Target
{
    public sealed class LocalTarget : IObjectTarget
    {
        public ILocalObjectStore LocalObjectStore { get; init; }

        private FSObject obj;
        private byte[] payload;
        private int offset;

        public void WriteHeader(FSObject header)
        {
            obj = header;
            payload = new byte[obj.PayloadSize];
            offset = 0;
        }

        public void WriteChunk(byte[] chunk)
        {
            chunk.CopyTo(payload, offset);
            offset += chunk.Length;
        }

        public AccessIdentifiers Close()
        {
            obj.Payload = ByteString.CopyFrom(payload);
            LocalObjectStore.Put(obj);
            return new()
            {
                Self = obj.ObjectId,
            };
        }

        public void Dispose() { }
    }
}
