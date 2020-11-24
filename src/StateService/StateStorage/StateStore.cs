using Akka.Actor;
using Neo.Cryptography.MPT;
using Neo.IO;
using Neo.IO.Caching;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins.StateService.StateStorage.LevelDB;
using Neo.Plugins.StateService.Validation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Neo.Plugins.StateService.StateStorage
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
            this.store = new Store(path);
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
                    OnNewStateRoot(state_root);
                    break;
                case StorageChanges changes:
                    UpdateLocalStateRoot(changes.Height, changes.ChangeSet);
                    break;
                default:
                    break;
            }
        }

        private void OnNewStateRoot(StateRoot state_root)
        {
            if (state_root?.Witness is null) return;
            if (state_root.Index <= ValidatedRootIndex) return;
            if (LocalRootIndex < state_root.Index && state_root.Index < LocalRootIndex + MaxCacheCount)
            {
                cache.Add(state_root.Index, state_root);
                return;
            }
            using var mpt_snapshot = Singleton.GetSnapshot();
            StateRoot local_root = mpt_snapshot.StateRoots.GetAndChange(state_root.Index);
            if (local_root is null || local_root.Witness != null) return;
            using var snapshot = Blockchain.Singleton.GetSnapshot();
            if (!state_root.Verify(snapshot)) return;
            if (local_root.RootHash != state_root.RootHash) return;
            local_root.Witness = state_root.Witness;
            HashIndexState validated = mpt_snapshot.ValidatedHashIndex.GetAndChange();
            validated.Index = state_root.Index;
            validated.Hash = state_root.RootHash;
            mpt_snapshot.Commit();
            system.Validation?.Tell(new ValidationService.ValidatedRootPersisted { Index = state_root.Index });
            core?.LocalNode.Tell(Message.Create(MessageCommand.StateRoot, state_root));
        }

        private void UpdateLocalStateRoot(uint height, List<Item> change_set)
        {
            using StateSnapshot mpt_snapshot = Singleton.GetSnapshot();
            foreach (var item in change_set)
            {
                switch (item.State)
                {
                    case TrackState.Added:
                        mpt_snapshot.Trie.Put(item.Key, item.Value);
                        break;
                    case TrackState.Changed:
                        mpt_snapshot.Trie.Put(item.Key, item.Value);
                        break;
                    case TrackState.Deleted:
                        mpt_snapshot.Trie.Delete(item.Key);
                        break;
                }
            }
            mpt_snapshot.UpdateStateRoot(height);
            mpt_snapshot.Commit();
            system.Validation?.Tell(new ValidationService.BlockPersisted { Index = height });
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
