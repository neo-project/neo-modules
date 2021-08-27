using System.Security.Cryptography;
using Neo.FileStorage.API.Session;
using FSObject = Neo.FileStorage.API.Object.Object;
using FSVersion = Neo.FileStorage.API.Refs.Version;

namespace Neo.FileStorage.Storage.Services.Object.Put.Target
{
    public sealed class FormatTarget : IObjectTarget
    {
        public ECDsa Key { get; init; }
        public IObjectTarget Next { get; init; }
        public SessionToken SessionToken { get; init; }
        public IEpochSource EpochSource { get; init; }

        private FSObject obj;
        private ulong size;

        public void WriteHeader(FSObject obj)
        {
            this.obj = obj;
            size = 0;
        }

        public void WriteChunk(byte[] chunk)
        {
            size += (ulong)chunk.Length;
            Next.WriteChunk(chunk);
        }

        public AccessIdentifiers Close()
        {
            obj.Header.Version = FSVersion.SDKVersion();
            obj.Header.PayloadLength = size;
            obj.Header.SessionToken = SessionToken;
            obj.Header.CreationEpoch = EpochSource.CurrentEpoch;
            obj.ObjectId = obj.CalculateID();
            if (obj.Parent is not null && obj.Parent.Signature is null)
            {
                var parent = obj.Parent;
                parent.Header.SessionToken = SessionToken;
                parent.Header.CreationEpoch = EpochSource.CurrentEpoch;
                parent.ObjectId = parent.CalculateID();
                parent.Signature = parent.CalculateIDSignature(Key);
                obj.Header.Split.Parent = parent.CalculateID();
                obj.Header.Split.ParentSignature = parent.Signature;
                obj.Header.Split.ParentHeader = parent.Header;
            }
            obj.Signature = obj.CalculateIDSignature(Key);
            Next.WriteHeader(obj);
            Next.Close();
            return new AccessIdentifiers
            {
                Self = obj.ObjectId,
                Parent = obj.ParentId,
                ParentHeader = obj.Parent,
            };
        }

        public void Dispose()
        {
            Next?.Dispose();
        }
    }
}
