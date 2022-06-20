using System;
using ZstdNet;

namespace Neo.FileStorage.Storage.LocalObjectStorage.Blobstor
{
    public sealed class ZstdCompressor : ICompressor
    {
        private static readonly byte[] ZstdFrameMagic = new byte[] { 0x28, 0xb5, 0x2f, 0xfd };

        public byte[] Compress(byte[] data)
        {
            using Compressor compressor = new();
            return compressor.Wrap(data);
        }

        public byte[] Decompress(byte[] data)
        {
            using Decompressor decompressor = new();
            return decompressor.Unwrap(data);
        }

        public bool IsCompressed(byte[] data)
        {
            if (data.Length < ZstdFrameMagic.Length) return false;
            return data.AsSpan().StartsWith(ZstdFrameMagic);
        }
    }
}
