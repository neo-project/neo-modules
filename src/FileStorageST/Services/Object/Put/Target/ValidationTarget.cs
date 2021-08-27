using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Storage.Core.Object;
using System;
using System.Linq;
using System.Security.Cryptography;
using FSObject = Neo.FileStorage.API.Object.Object;
using static Neo.Helper;

namespace Neo.FileStorage.Storage.Services.Object.Put.Target
{
    public sealed class ValidationTarget : IObjectTarget
    {
        public IObjectTarget Next { get; init; }
        public IObjectValidator ObjectValidator { get; init; }
        public ulong MaxObjectSize { get; init; }

        private HashAlgorithm hasher;
        private ulong payloadSize;
        private ulong writtenPayload = 0;
        private Checksum checksum;
        private byte[] payload = Array.Empty<byte>();

        public void WriteHeader(FSObject header)
        {
            payloadSize = header.PayloadSize;
            var chunk_len = (ulong)header.Payload.Length;
            if (payloadSize < chunk_len)
                throw new InvalidOperationException("wrong payload size");
            if (MaxObjectSize < payloadSize)
                throw new InvalidOperationException("payload size is greater than the limit");
            checksum = header.PayloadChecksum;
            if (checksum.Type != ChecksumType.Sha256)
                throw new InvalidOperationException("unsupported payload checksum type " + checksum.Type);
            var vr = ObjectValidator.Validate(header);
            if (vr != VerifyResult.Success)
                throw new FormatException($"invalid object {vr}");
            hasher = SHA256.Create();
            hasher.Initialize();
            Next.WriteHeader(header);
            if (0 < chunk_len)
            {
                writtenPayload += chunk_len;
                hasher.TransformBlock(header.Payload.ToByteArray(), 0, header.Payload.Length, null, 0);
            }
        }

        public void WriteChunk(byte[] chunk)
        {
            var chunk_len = (ulong)chunk.Length;
            if (payloadSize < writtenPayload + chunk_len)
                throw new InvalidOperationException("wrong payload size");
            payload = Concat(payload, chunk);
            Next.WriteChunk(chunk); //TODO: fix this may double payload memory
            writtenPayload += chunk_len;
            if (writtenPayload == payloadSize)
                hasher.TransformFinalBlock(chunk, 0, chunk.Length);
            else
                hasher.TransformBlock(chunk, 0, chunk.Length, null, 0);
        }

        public AccessIdentifiers Close()
        {
            if (!checksum.Sum.ToByteArray().SequenceEqual(hasher.Hash))
                throw new InvalidOperationException(nameof(ValidationTarget) + " invalid checksum");
            return Next.Close();
        }

        public void Dispose()
        {
            Next?.Dispose();
            hasher?.Dispose();
        }
    }
}
