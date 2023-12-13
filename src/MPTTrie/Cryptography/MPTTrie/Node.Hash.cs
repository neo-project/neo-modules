// Copyright (C) 2015-2023 The Neo Project.
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
                _type = NodeType.HashNode,
                _hash = hash,
            };
            return n;
        }

        protected int HashSize => _hash.Size;

        private void SerializeHash(BinaryWriter writer)
        {
            writer.Write(_hash);
        }

        private void DeserializeHash(ref MemoryReader reader)
        {
            _hash = reader.ReadSerializable<UInt256>();
        }
    }
}
