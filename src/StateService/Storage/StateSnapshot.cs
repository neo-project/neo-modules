using Neo;
using Neo.IO;
using Neo.IO.Caching;
using Neo.Ledger;
using Neo.Persistence;
using Neo.Plugins.MPT;
using System;
using System.Text;

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
            return snapshot.TryGet(Prefixs.StateRoot, BitConverter.GetBytes(index))?.AsSerializable<StateRoot>();
        }

        public void AddLocalStateRoot(StateRoot state_root)
        {
            snapshot.Put(Prefixs.StateRoot, BitConverter.GetBytes(state_root.Index), state_root.ToArray());
            snapshot.Put(Prefixs.CurrentLocalRootIndex, Array.Empty<byte>(), BitConverter.GetBytes(state_root.Index));
        }

        public uint CurrentLocalRootIndex()
        {
            var bytes = snapshot.TryGet(Prefixs.CurrentLocalRootIndex, Array.Empty<byte>());
            if (bytes is null) return uint.MaxValue;
            return BitConverter.ToUInt32(bytes);
        }

        public UInt256 CurrentLocalRootHash()
        {
            var index = CurrentLocalRootIndex();
            if (index == uint.MaxValue) return null;
            return GetStateRoot(index)?.RootHash;
        }

        public void AddValidatedStateRoot(StateRoot state_root)
        {
            if (state_root?.Witness is null)
                throw new ArgumentException(nameof(state_root) + " missing witness in invalidated state root");
            snapshot.Put(Prefixs.StateRoot, BitConverter.GetBytes(state_root.Index), state_root.ToArray());
            snapshot.Put(Prefixs.CurrentValidatedRootIndex, Array.Empty<byte>(), BitConverter.GetBytes(state_root.Index));
        }

        public uint CurrentValidatedRootIndex()
        {
            var bytes = snapshot.TryGet(Prefixs.CurrentValidatedRootIndex, Array.Empty<byte>());
            if (bytes is null) return uint.MaxValue;
            return BitConverter.ToUInt32(bytes);
        }

        public UInt256 CurrentValidatedRootHash()
        {
            var index = CurrentLocalRootIndex();
            if (index == uint.MaxValue) return null;
            var state_root = GetStateRoot(index);
            if (state_root is null || state_root.Witness is null)
                throw new InvalidOperationException(nameof(CurrentValidatedRootHash) + " could not get validated state root");
            return state_root.RootHash;
        }

        public void Commit()
        {
            snapshot.Commit();
        }

        public void Dispose()
        {
            snapshot.Dispose();
        }
    }
}
