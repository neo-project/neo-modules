using System;
using Google.Protobuf;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.Services.Object.Put.Target
{
    public sealed class LocalTarget : IObjectTarget
    {
        public ILocalObjectStore LocalObjectStore { get; init; }

        private FSObject obj;

        public void WriteHeader(FSObject header)
        {
            obj = header.Clone();
        }

        public void WriteChunk(byte[] chunk)
        {
            obj.Payload = obj.Payload.Concat(ByteString.CopyFrom(chunk));
        }

        public AccessIdentifiers Close()
        {
            LocalObjectStore.Put(obj);
            return new()
            {
                Self = obj.ObjectId,
            };
        }

        public void Dispose() { }
    }
}
