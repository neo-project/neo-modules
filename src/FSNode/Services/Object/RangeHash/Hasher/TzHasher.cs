using System;

namespace Neo.FSNode.Services.Object.RangeHash.Hasher
{
    public class TzHasher : IHasher
    {
        private byte[] data = Array.Empty<byte>();

        public void Add(byte[] chunk)
        {

        }
        public byte[] Sum()
        {
            return data;
        }
    }
}
