using Neo.FileStorage.API.Refs;
using Neo.Cryptography;
using Neo.FileStorage.Utils;
using System;
using static Neo.Helper;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Services.Object.Get.Writer
{
    public class RangeHashWriter : IObjectWriter
    {
        private ChecksumType type;
        private byte[] data = Array.Empty<byte>();

        public RangeHashWriter(ChecksumType type)
        {
            this.type = type;
        }

        public void WriteHeader(FSObject obj)
        {
            throw new NotImplementedException(nameof(WriteHeader));
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
