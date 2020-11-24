using Neo.Cryptography.MPT;
using Neo.IO;
using Neo.IO.Caching;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using System;

namespace Neo.Plugins.MPTService.MPTStorage
{
    public class MPTSnapshot : IDisposable
    {
        private readonly ISnapshot snapshot;
        public MetaDataCache<HashIndexState> LocalRootHashIndex;
        public MetaDataCache<HashIndexState> ValidatedHashIndex;
        public DataCache<SerializableWrapper<uint>, StateRoot> StateRoots;
        public MPTTrie<StorageKey, StorageItem> Trie;

        public long StateHeight => ValidatedHashIndex.Get() is null ? -1 : (long)ValidatedHashIndex.Get().Index;
        public UInt256 CurrentLocalRootHash => LocalRootHashIndex.Get().Hash;

        public MPTSnapshot(IStore store)
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

        public void UpdateStateRoot(uint height)
        {
            UInt256 root_hash = Trie.Root.Hash;
            StateRoot state_root = new StateRoot
            {
                Index = height,
                RootHash = root_hash,
                Witness = null,
            };
            HashIndexState current_root = LocalRootHashIndex.GetAndChange();
            current_root.Index = height;
            current_root.Hash = root_hash;
            StateRoots.Add(height, state_root);
        }

        public void Dispose()
        {
            snapshot.Dispose();
        }
    }
}
