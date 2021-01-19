using System;
using Neo.Cryptography;
using static Neo.Helper;

namespace Neo.FSNode.Services.Object.RangeHash.Hasher
{
    public class Sha256Hasher : IHasher
    {
        private byte[] data = Array.Empty<byte>();

        public void Add(byte[] chunk)
        {
            data = Concat(data, chunk);
        }
        public byte[] Sum()
        {
            return data.Sha256();
        }
    }
}
