using Google.Protobuf;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Storage.Services.Object.Util;
using Neo.FileStorage.Storage.Services.Object.Put.Remote;
using System.Threading;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.Services.Object.Put.Target
{
    public sealed class RemoteTarget : IObjectTarget
    {
        public CancellationToken Cancellation { get; init; }
        public KeyStore KeyStorage { get; init; }
        public PutInitPrm Prm { get; init; }
        public NodeInfo Node { get; init; }
        public IPutClientCache PutClientCache { get; init; }

        private FSObject obj;

        public void WriteHeader(FSObject header)
        {
            obj = header;
        }

        public void WriteChunk(byte[] chunk)
        {
            obj.Payload = obj.Payload.Concat(ByteString.CopyFrom(chunk));
        }

        public AccessIdentifiers Close()
        {
            var key = KeyStorage.GetKey(Prm?.SessionToken);
            var client = PutClientCache.Get(Node);
            var id = client.PutObject(obj, new API.Client.CallOptions().WithTTL(1).WithKey(key), Cancellation).Result;
            return new()
            {
                Self = id,
            };
        }

        public void Dispose() { }
    }
}
