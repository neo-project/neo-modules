using NeoFS.API.v2.Refs;
using Neo.Cryptography;
using Neo.FSNode.Utils;
using System;
using static Neo.Helper;

namespace Neo.FSNode.Services.Object.Get.Writer
{
    public class RangeHashWriter : IChunkWriter
    {
        private ChecksumType type;
        private byte[] data = Array.Empty<byte>();

        public RangeHashWriter(ChecksumType type)
        {
            this.type = type;
        }

        public void WriteChunk(byte[] chunk)
        {
            data = Concat(data, chunk);
        }

        public byte[] GetHash()
        {
            switch (type)
            {
                case ChecksumType.Sha256:
                    return data.Sha256();
                case ChecksumType.Tz:
                    return data.Tz();
                default:
                    throw new InvalidOperationException(nameof(RangeHashWriter) + " unsupported hash type");
            }
        }
    }
}
