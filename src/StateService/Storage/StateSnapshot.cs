using Neo.IO;
using Neo.IO.Caching;
using Neo.Ledger;
using Neo.Persistence;
using Neo.Plugins.MPT;
using System;

namespace Neo.Plugins.StateService.Storage
{
    public class StateSnapshot : IDisposable
    {
        private readonly ISnapshot snapshot;
        public MetaDataCache<HashIndexState> LocalRootHashIndex;
        public MetaDataCache<HashIndexState> ValidatedHashIndex;
        public DataCache<SerializableWrapper<uint>, StateRoot> StateRoots;
        public MPTTrie<StorageKey, StorageItem> Trie;

        public long StateHeight => ValidatedHashIndex.Get() is null ? -1 : (long)ValidatedHashIndex.Get().Index;
        public UInt256 CurrentLocalRootHash => LocalRootHashIndex.Get().Hash;

        public StateSnapshot(IStore store)
        {
            snapshot = store.GetSnapshot();
            StateRoots = new StoreDataCache<SerializableWrapper<uint>, StateRoot>(snapshot, Prefixs.Roots);
            LocalRootHashIndex = new StoreMetaDataCache<HashIndexState>(snapshot, Prefixs.LocalRootIndex);
            ValidatedHashIndex = new StoreMetaDataCache<HashIndexState>(snapshot, Prefixs.ValidatedRootIndex);
            Trie = new MPTTrie<StorageKey, StorageItem>(snapshot, CurrentLocalRootHash == UInt256.Zero ? null : CurrentLocalRootHash, Settings.Default.FullState);
        }

        public void Commit()
        {
            Trie.Commit();
            StateRoots.Commit();
            LocalRootHashIndex.Commit();
            ValidatedHashIndex.Commit();
            snapshot.Commit();
        }

        public void Dispose()
        {
            snapshot.Dispose();
        }
    }
}
