using System.Threading;
using Google.Protobuf;
using Neo.FileStorage.Storage.Services.Object.Util;
using FSObject = Neo.FileStorage.API.Object.Object;
using System.Collections.Generic;
using Neo.FileStorage.Storage.Services.Object.Put.Remote;

namespace Neo.FileStorage.Storage.Services.Object.Put.Target
{
    public sealed class RemoteTarget : IObjectTarget
    {

        public CancellationToken Token { get; init; }
        public KeyStore KeyStorage { get; init; }
        public PutInitPrm Prm { get; init; }
        public List<Network.Address> Addresses { get; init; }
        public IPutClientCache PutClientCache { get; init; }

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
            var key = KeyStorage.GetKey(Prm?.SessionToken);
            var client = PutClientCache.Get(Addresses);
            var id = client.PutObject(obj, Prm.CallOptions.WithTTL(1).WithKey(key), Token).Result;
            return new()
            {
                Self = id,
            };
        }

        public void Dispose() { }
    }
}
