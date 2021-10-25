using Neo.IO;
using System;
using System.IO;

namespace Neo.Cryptography.MPTTrie
{
    partial class Node
    {
        public static Node NewHash(UInt256 hash)
        {
            if (hash is null) throw new ArgumentNullException(nameof(NewHash));
            var n = new Node
            {
                type = NodeType.HashNode,
                hash = hash,
            };
            return n;
        }

        protected int HashSize => hash.Size;

        private void SerializeHash(BinaryWriter writer)
        {
            writer.Write(hash);
        }

        private void DeserializeHash(BinaryReader reader)
        {
            hash = reader.ReadSerializable<UInt256>();
        }
    }
}
