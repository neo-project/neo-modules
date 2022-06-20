using Neo.FileStorage.API.Cryptography.Tz;
using Neo.FileStorage.API.Refs;
using System;
using System.Security.Cryptography;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.Services.Object.Get.Writer
{
    public class RangeHashWriter : IObjectResponseWriter
    {
        private HashAlgorithm hasher;

        public RangeHashWriter(ChecksumType type)
        {
            hasher = type switch
            {
                ChecksumType.Sha256 => SHA256.Create(),
                ChecksumType.Tz => new TzHash(),
                _ => throw new InvalidOperationException(nameof(RangeHashWriter) + " unsupported hash type")
            };
        }

        public void WriteHeader(FSObject obj)
        {
            throw new InvalidOperationException($"{nameof(RangeHashWriter)} should not write header when get range hash");
        }

        public void WriteChunk(byte[] chunk)
        {
            hasher.TransformBlock(chunk, 0, chunk.Length, null, 0);
        }

        public byte[] GetHash()
        {
            hasher.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return hasher.Hash;
        }
    }
}
