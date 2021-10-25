using Neo.IO;
using Neo.SmartContract;
using System;
using System.IO;

namespace Neo.Cryptography.MPTTrie
{
    partial class Node
    {
        public const int MaxValueLength = 3 + ApplicationEngine.MaxStorageValueSize + sizeof(bool);
        public byte[] Value;

        public static Node NewLeaf(byte[] value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));
            var n = new Node
            {
                type = NodeType.LeafNode,
                Value = value,
                Reference = 1,
            };
            return n;
        }

        protected int LeafSize => Value.GetVarSize();

        private void SerializeLeaf(BinaryWriter writer)
        {
            writer.WriteVarBytes(Value);
        }

        private void DeserializeLeaf(BinaryReader reader)
        {
            Value = reader.ReadVarBytes();
        }
    }
}
