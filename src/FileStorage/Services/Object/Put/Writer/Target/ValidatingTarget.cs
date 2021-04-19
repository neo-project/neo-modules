using Google.Protobuf;
using Neo.FileStorage.Core.Object;
using Neo.FileStorage.Services.ObjectManager.Transformer;
using Neo.FileStorage.API.Refs;
using System;
using static Neo.Helper;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Services.Object.Put
{
    public class ValidatingTarget : IObjectTarget
    {
        public IObjectTarget Next { get; init; }
        public ObjectValidator ObjectValidator { get; init; }
        private Checksum checksum;
        private byte[] payload = Array.Empty<byte>();

        public void WriteHeader(FSObject header)
        {
            checksum = header.Header.PayloadHash;
            if (checksum.Type != ChecksumType.Sha256 && checksum.Type != ChecksumType.Tz)
                throw new InvalidOperationException(nameof(ValidatingTarget) + " unsupported paylaod checksum type " + checksum.Type);
            if (!ObjectValidator.Validate(header))
                throw new FormatException(nameof(ValidatingTarget) + " invalid object");
            Next.WriteHeader(header);
        }

        public void WriteChunk(byte[] chunk)
        {
            payload = Concat(payload, chunk);
            Next.WriteChunk(chunk); //TODO: fix this may double payload memory
        }

        public AccessIdentifiers Close()
        {
            if (!checksum.Verify(ByteString.CopyFrom(payload))) throw new InvalidOperationException(nameof(ValidatingTarget) + " invalid checksum");
            return Next.Close();
        }
    }
}
