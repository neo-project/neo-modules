using System.IO;

namespace Neo.Cryptography.MPTTrie.Tests
{
    public static class Helper
    {
        private static readonly byte Prefix = 0xf0;

        public static byte[] ToKey(this UInt256 hash)
        {
            byte[] buffer = new byte[UInt256.Length + 1];
            using (MemoryStream ms = new MemoryStream(buffer, true))
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                writer.Write(Prefix);
                hash.Serialize(writer);
            }
            return buffer;
        }
    }
}
