// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.StateService is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Cryptography.MPTTrie;
using Neo.IO;
using Neo.Persistence;
using Neo.Plugins.StateService.Network;
using System;

namespace Neo.Plugins.StateService.Storage
{
    class StateSnapshot : IDisposable
    {
        private readonly ISnapshot _snapshot;
        public Trie Trie;

        public StateSnapshot(IStore store)
        {
            _snapshot = store.GetSnapshot();
            Trie = new Trie(_snapshot, CurrentLocalRootHash(), Settings.Default.FullState);
        }

        public StateRoot GetStateRoot(uint index)
        {
            return _snapshot.TryGet(Keys.StateRoot(index))?.AsSerializable<StateRoot>();
        }

        public void AddLocalStateRoot(StateRoot stateRoot)
        {
            _snapshot.Put(Keys.StateRoot(stateRoot.Index), stateRoot.ToArray());
            _snapshot.Put(Keys.CurrentLocalRootIndex, BitConverter.GetBytes(stateRoot.Index));
        }

        public uint? CurrentLocalRootIndex()
        {
            var bytes = _snapshot.TryGet(Keys.CurrentLocalRootIndex);
            if (bytes is null) return null;
            return BitConverter.ToUInt32(bytes);
        }

        public UInt256 CurrentLocalRootHash()
        {
            var index = CurrentLocalRootIndex();
            if (index is null) return null;
            return GetStateRoot((uint)index)?.RootHash;
        }

        public void AddValidatedStateRoot(StateRoot stateRoot)
        {
            if (stateRoot?.Witness is null)
                throw new ArgumentException(nameof(stateRoot) + " missing witness in invalidated state root");
            _snapshot.Put(Keys.StateRoot(stateRoot.Index), stateRoot.ToArray());
            _snapshot.Put(Keys.CurrentValidatedRootIndex, BitConverter.GetBytes(stateRoot.Index));
        }

        public uint? CurrentValidatedRootIndex()
        {
            var bytes = _snapshot.TryGet(Keys.CurrentValidatedRootIndex);
            if (bytes is null) return null;
            return BitConverter.ToUInt32(bytes);
        }

        public UInt256 CurrentValidatedRootHash()
        {
            var index = CurrentLocalRootIndex();
            if (index is null) return null;
            var stateRoot = GetStateRoot((uint)index);
            if (stateRoot is null || stateRoot.Witness is null)
                throw new InvalidOperationException(nameof(CurrentValidatedRootHash) + " could not get validated state root");
            return stateRoot.RootHash;
        }

        public void Commit()
        {
            Trie.Commit();
            _snapshot.Commit();
        }

        public void Dispose()
        {
            _snapshot.Dispose();
        }
    }
}
