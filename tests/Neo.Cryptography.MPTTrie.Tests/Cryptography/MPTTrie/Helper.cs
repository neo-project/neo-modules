using Neo.SmartContract;
using System.Collections.Generic;
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

        public static byte[] Get(this Trie trie, byte[] key)
        {
            var skey = new StorageKey { Key = key };
            var sitem = trie[skey];
            return sitem.Value.ToArray();
        }

        public static void Put(this Trie trie, byte[] key, byte[] value)
        {
            var skey = new StorageKey { Key = key };
            var sitem = new StorageItem(value);
            trie.Put(skey, sitem);
        }

        public static bool Delete(this Trie trie, byte[] key)
        {
            var skey = new StorageKey { Key = key };
            return trie.Delete(skey);
        }

        public static bool TryGetProof(this Trie trie, byte[] key, out HashSet<byte[]> proof)
        {
            var skey = new StorageKey { Key = key };
            return trie.TryGetProof(skey, out proof);
        }
    }
}
