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
using Neo.Persistence;
using System.Collections.Generic;
using System.IO;

namespace Neo.Cryptography.MPTTrie
{
    public class Cache
    {
        private enum TrackState : byte
        {
            None,
            Added,
            Changed,
            Deleted
        }

        private class Trackable
        {
            public Node Node;
            public TrackState State;
        }

        private readonly ISnapshot _store;
        private readonly byte _prefix;
        private readonly Dictionary<UInt256, Trackable> _cache = new Dictionary<UInt256, Trackable>();

        public Cache(ISnapshot store, byte prefix)
        {
            this._store = store;
            this._prefix = prefix;
        }

        private byte[] Key(UInt256 hash)
        {
            byte[] buffer = new byte[UInt256.Length + 1];
            using (MemoryStream ms = new MemoryStream(buffer, true))
            using (BinaryWriter writer = new BinaryWriter(ms))
            {
                writer.Write(_prefix);
                hash.Serialize(writer);
            }
            return buffer;
        }

        public Node Resolve(UInt256 hash)
        {
            if (_cache.TryGetValue(hash, out Trackable t))
            {
                return t.Node?.Clone();
            }
            var n = _store.TryGet(Key(hash))?.AsSerializable<Node>();
            _cache.Add(hash, new Trackable
            {
                Node = n,
                State = TrackState.None,
            });
            return n?.Clone();
        }

        public void PutNode(Node np)
        {
            var n = Resolve(np.Hash);
            if (n is null)
            {
                np.Reference = 1;
                _cache[np.Hash] = new Trackable
                {
                    Node = np.Clone(),
                    State = TrackState.Added,
                };
                return;
            }
            var entry = _cache[np.Hash];
            entry.Node.Reference++;
            entry.State = TrackState.Changed;
        }

        public void DeleteNode(UInt256 hash)
        {
            var n = Resolve(hash);
            if (n is null) return;
            if (1 < n.Reference)
            {
                var entry = _cache[hash];
                entry.Node.Reference--;
                entry.State = TrackState.Changed;
                return;
            }
            _cache[hash] = new Trackable
            {
                Node = null,
                State = TrackState.Deleted,
            };
        }

        public void Commit()
        {
            foreach (var item in _cache)
            {
                switch (item.Value.State)
                {
                    case TrackState.Added:
                    case TrackState.Changed:
                        _store.Put(Key(item.Key), item.Value.Node.ToArray());
                        break;
                    case TrackState.Deleted:
                        _store.Delete(Key(item.Key));
                        break;
                }
            }
            _cache.Clear();
        }
    }
}
