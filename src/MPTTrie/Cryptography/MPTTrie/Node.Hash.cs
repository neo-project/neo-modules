// Copyright (C) 2015-2022 The Neo Project.
//
// The Neo.Cryptography.MPT is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

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

        private void DeserializeHash(ref MemoryReader reader)
        {
            hash = reader.ReadSerializable<UInt256>();
        }
    }
}
