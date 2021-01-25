using Neo.IO;
using Neo.Persistence;
using Neo.Plugins.MPT;
using Neo.SmartContract;
using System;

namespace Neo.Plugins.StateService.Storage
{
    public class StateSnapshot : IDisposable
    {
        private readonly ISnapshot snapshot;
        public MPTTrie<StorageKey, StorageItem> Trie;

        public StateSnapshot(IStore store)
        {
            snapshot = store.GetSnapshot();
            Trie = new MPTTrie<StorageKey, StorageItem>(snapshot, CurrentLocalRootHash(), Settings.Default.FullState);
        }

        public StateRoot GetStateRoot(uint index)
        {
            return snapshot.TryGet(Keys.StateRoot(index))?.AsSerializable<StateRoot>();
        }

        public void AddLocalStateRoot(StateRoot state_root)
        {
            snapshot.Put(Keys.StateRoot(state_root.Index), state_root.ToArray());
            snapshot.Put(Keys.CurrentLocalRootIndex, BitConverter.GetBytes(state_root.Index));
        }

        public uint? CurrentLocalRootIndex()
        {
            var bytes = snapshot.TryGet(Keys.CurrentLocalRootIndex);
            if (bytes is null) return null;
            return BitConverter.ToUInt32(bytes);
        }

        public UInt256 CurrentLocalRootHash()
        {
            var index = CurrentLocalRootIndex();
            if (index is null) return null;
            return GetStateRoot((uint)index)?.RootHash;
        }

        public void AddValidatedStateRoot(StateRoot state_root)
        {
            if (state_root?.Witness is null)
                throw new ArgumentException(nameof(state_root) + " missing witness in invalidated state root");
            snapshot.Put(Keys.StateRoot(state_root.Index), state_root.ToArray());
            snapshot.Put(Keys.CurrentValidatedRootIndex, BitConverter.GetBytes(state_root.Index));
        }

        public uint? CurrentValidatedRootIndex()
        {
            var bytes = snapshot.TryGet(Keys.CurrentValidatedRootIndex);
            if (bytes is null) return null;
            return BitConverter.ToUInt32(bytes);
        }

        public UInt256 CurrentValidatedRootHash()
        {
            var index = CurrentLocalRootIndex();
            if (index is null) return null;
            var state_root = GetStateRoot((uint)index);
            if (state_root is null || state_root.Witness is null)
                throw new InvalidOperationException(nameof(CurrentValidatedRootHash) + " could not get validated state root");
            return state_root.RootHash;
        }

        public void Commit()
        {
            Trie.Commit();
            snapshot.Commit();
        }

        public void Dispose()
        {
            snapshot.Dispose();
        }
    }
}
