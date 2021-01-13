using Akka.Actor;
using Neo.IO;
using Neo.IO.Caching;
using Neo.Ledger;
using Neo.Persistence;
using Neo.Plugins.MPT;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Neo.Plugins.StateService.Storage
{
    public class StateStore : UntypedActor
    {
        public class Item { public TrackState State; public StorageKey Key; public StorageItem Value; }
        public class StorageChanges { public uint Height; public List<Item> ChangeSet; }
        private readonly StatePlugin system;
        private readonly NeoSystem core;
        private readonly IStore store;
        private const int MaxCacheCount = 100;
        private readonly Dictionary<uint, StateRoot> cache = new Dictionary<uint, StateRoot>();
        public MetaDataCache<HashIndexState> LocalRootHashIndex => new StoreMetaDataCache<HashIndexState>(store, Prefixs.LocalRootIndex);
        public MetaDataCache<HashIndexState> ValidatedHashIndex => new StoreMetaDataCache<HashIndexState>(store, Prefixs.ValidatedRootIndex);
        public DataCache<SerializableWrapper<uint>, StateRoot> StateRoots => new StoreDataCache<SerializableWrapper<uint>, StateRoot>(store, Prefixs.Roots);

        public UInt256 CurrentLocalRootHash => LocalRootHashIndex.Get()?.Hash;
        public uint LocalRootIndex => LocalRootHashIndex.Get()?.Index ?? 0;
        public long ValidatedRootIndex => ValidatedHashIndex.Get().Index == uint.MaxValue ? -1 : (long)ValidatedHashIndex.Get().Index;

        private static StateStore singleton;
        public static StateStore Singleton
        {
            get
            {
                while (singleton is null) Thread.Sleep(10);
                return singleton;
            }
        }

        public StateStore(StatePlugin system, NeoSystem core, string path)
        {
            if (singleton != null) throw new InvalidOperationException(nameof(StateStore));
            this.system = system;
            this.core = core;
            this.store = core.LoadStore(path);
            singleton = this;
        }

        public void Dispose()
        {
            store.Dispose();
        }

        public StateSnapshot GetSnapshot()
        {
            return new StateSnapshot(store);
        }

        public HashSet<byte[]> GetProof(UInt256 root, StorageKey skey)
        {
            using ISnapshot snapshot = store.GetSnapshot();
            var trie = new MPTTrie<StorageKey, StorageItem>(snapshot, root);
            return trie.GetProof(skey);
        }

        public MetaDataCache<HashIndexState> GetLocalRootHashIndex()
        {
            return new StoreMetaDataCache<HashIndexState>(store, Prefixs.LocalRootIndex);
        }

        public MetaDataCache<HashIndexState> GetValidatedHashIndex()
        {
            return new StoreMetaDataCache<HashIndexState>(store, Prefixs.ValidatedRootIndex);
        }

        public DataCache<SerializableWrapper<uint>, StateRoot> GetStateRoots()
        {
            return new StoreDataCache<SerializableWrapper<uint>, StateRoot>(store, Prefixs.Roots);
        }

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case StateRoot state_root:
                    Sender.Tell(OnNewStateRoot(state_root));
                    break;
                case StorageChanges changes:
                    UpdateLocalStateRoot(changes.Height, changes.ChangeSet);
                    break;
                default:
                    break;
            }
        }

        private bool OnNewStateRoot(StateRoot state_root)
        {
            if (state_root?.Witness is null) return false;
            if (state_root.Index <= ValidatedRootIndex) return false;
            if (LocalRootIndex < state_root.Index && state_root.Index < LocalRootIndex + MaxCacheCount)
            {
                cache.Add(state_root.Index, state_root);
                return true;
            }
            using var state_snapshot = Singleton.GetSnapshot();
            StateRoot local_root = state_snapshot.StateRoots.GetAndChange(state_root.Index);
            if (local_root is null || local_root.Witness != null) return false;
            using var snapshot = Blockchain.Singleton.GetSnapshot();
            if (!state_root.Verify(snapshot)) return false;
            if (local_root.RootHash != state_root.RootHash) return false;
            local_root.Witness = state_root.Witness;
            HashIndexState validated = state_snapshot.ValidatedHashIndex.GetAndChange();
            validated.Index = state_root.Index;
            validated.Hash = state_root.RootHash;
            state_snapshot.Commit();
            //Tell validation service
            return true;
        }

        private void UpdateLocalStateRoot(uint height, List<Item> change_set)
        {
            using StateSnapshot state_snapshot = Singleton.GetSnapshot();
            foreach (var item in change_set)
            {
                switch (item.State)
                {
                    case TrackState.Added:
                        state_snapshot.Trie.Put(item.Key, item.Value);
                        break;
                    case TrackState.Changed:
                        state_snapshot.Trie.Put(item.Key, item.Value);
                        break;
                    case TrackState.Deleted:
                        state_snapshot.Trie.Delete(item.Key);
                        break;
                }
            }
            UInt256 root_hash = state_snapshot.Trie.Root.Hash;
            StateRoot state_root = new StateRoot
            {
                Index = height,
                RootHash = root_hash,
                Witness = null,
            };
            HashIndexState current_root = state_snapshot.LocalRootHashIndex.GetAndChange();
            current_root.Index = height;
            current_root.Hash = root_hash;
            state_snapshot.StateRoots.Add(height, state_root);
            state_snapshot.Commit();
            CheckValidatedStateRoot(height);
        }

        private void CheckValidatedStateRoot(uint index)
        {
            if (cache.TryGetValue(index, out StateRoot state_root))
            {
                cache.Remove(index);
                OnNewStateRoot(state_root);
            }
        }

        protected override void PostStop()
        {
            base.PostStop();
        }

        public static Props Props(StatePlugin system, NeoSystem core, string path)
        {
            return Akka.Actor.Props.Create(() => new StateStore(system, core, path));
        }
    }
}
