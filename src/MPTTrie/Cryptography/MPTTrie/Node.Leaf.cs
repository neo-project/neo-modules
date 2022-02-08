// Copyright (C) 2015-2021 The Neo Project.
//
// The Neo.Cryptography.MPT is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

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
