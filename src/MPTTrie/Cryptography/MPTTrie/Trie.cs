// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Cryptography.MPT is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Persistence;
using System;

namespace Neo.Cryptography.MPTTrie
{
    public partial class Trie
    {
        private const byte Prefix = 0xf0;
        private readonly bool _full;
        private readonly ISnapshot _store;
        private Node _root;
        private readonly Cache _cache;
        public Node Root => _root;

        public Trie(ISnapshot store, UInt256 root, bool fullState = false)
        {
            this._store = store ?? throw new ArgumentNullException(nameof(store));
            this._cache = new Cache(store, Prefix);
            this._root = root is null ? new Node() : Node.NewHash(root);
            this._full = fullState;
        }

        private static byte[] ToNibbles(ReadOnlySpan<byte> path)
        {
            var result = new byte[path.Length * 2];
            for (int i = 0; i < path.Length; i++)
            {
                result[i * 2] = (byte)(path[i] >> 4);
                result[i * 2 + 1] = (byte)(path[i] & 0x0F);
            }
            return result;
        }

        private static byte[] FromNibbles(ReadOnlySpan<byte> path)
        {
            if (path.Length % 2 != 0) throw new FormatException($"MPTTrie.FromNibbles invalid path.");
            var key = new byte[path.Length / 2];
            for (int i = 0; i < key.Length; i++)
            {
                key[i] = (byte)(path[i * 2] << 4);
                key[i] |= path[i * 2 + 1];
            }
            return key;
        }

        public void Commit()
        {
            _cache.Commit();
        }
    }
}
