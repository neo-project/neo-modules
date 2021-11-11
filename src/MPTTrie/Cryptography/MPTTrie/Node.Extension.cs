using Neo.IO;
using Neo.SmartContract;
using System;
using System.IO;

namespace Neo.Cryptography.MPTTrie
{
    partial class Node
    {
        public const int MaxKeyLength = (ApplicationEngine.MaxStorageKeySize + sizeof(int)) * 2;
        public byte[] Key;
        public Node Next;

        public static Node NewExtension(byte[] key, Node next)
        {
            if (key is null || next is null) throw new ArgumentNullException(nameof(NewExtension));
            if (key.Length == 0) throw new InvalidOperationException(nameof(NewExtension));
            var n = new Node
            {
                type = NodeType.ExtensionNode,
                Key = key,
                Next = next,
                Reference = 1,
            };
            return n;
        }

        protected int ExtensionSize => Key.GetVarSize() + Next.SizeAsChild;

        private void SerializeExtension(BinaryWriter writer)
        {
            writer.WriteVarBytes(Key);
            Next.SerializeAsChild(writer);
        }

        private void DeserializeExtension(BinaryReader reader)
        {
            Key = reader.ReadVarBytes();
            var n = new Node();
            n.Deserialize(reader);
            Next = n;
        }
    }
}
