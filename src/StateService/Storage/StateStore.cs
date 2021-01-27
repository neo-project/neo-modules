using Akka.Actor;
using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins.MPT;
using Neo.Plugins.StateService.Verification;
using Neo.SmartContract;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Neo.Plugins.StateService.Storage
{
    class StateStore : UntypedActor
    {
        private readonly NeoSystem core;
        private readonly StatePlugin system;
        private readonly IStore store;
        private const int MaxCacheCount = 100;
        private readonly Dictionary<uint, StateRoot> cache = new Dictionary<uint, StateRoot>();
        private StateSnapshot currentSnapshot;
        public UInt256 CurrentLocalRootHash => currentSnapshot.CurrentLocalRootHash();
        public uint? LocalRootIndex => currentSnapshot.CurrentLocalRootIndex();
        public uint? ValidatedRootIndex => currentSnapshot.CurrentValidatedRootIndex();

        private static StateStore singleton;
        public static StateStore Singleton
        {
            get
            {
                while (singleton is null) Thread.Sleep(10);
                return singleton;
            }
        }

        public StateStore(NeoSystem core, StatePlugin system, string path)
        {
            if (singleton != null) throw new InvalidOperationException(nameof(StateStore));
            this.core = core;
            this.system = system;
            this.store = core.LoadStore(path);
            singleton = this;
            core.ActorSystem.EventStream.Subscribe(Self, typeof(Blockchain.RelayResult));
            UpdateCurrentSnapshot();
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

        protected override void OnReceive(object message)
        {
            switch (message)
            {
                case StateRoot state_root:
                    OnNewStateRoot(state_root);
                    break;
                case Blockchain.RelayResult rr:
                    if (rr.Result == VerifyResult.Succeed && rr.Inventory is ExtensiblePayload payload && payload.Category == StatePlugin.StatePayloadCategory)
                        OnStatePayload(payload);
                    break;
                default:
                    break;
            }
        }

        private void OnStatePayload(ExtensiblePayload payload)
        {
            StateRoot state_root = null;
            try
            {
                state_root = payload.Data?.AsSerializable<StateRoot>();
            }
            catch (Exception ex)
            {
                Utility.Log(nameof(StateStore), LogLevel.Warning, " invalid state root " + ex.Message);
                return;
            }
            if (state_root != null)
                OnNewStateRoot(state_root);
        }

        private bool OnNewStateRoot(StateRoot state_root)
        {
            if (state_root?.Witness is null) return false;
            if (ValidatedRootIndex != null && state_root.Index <= ValidatedRootIndex) return false;
            if (LocalRootIndex is null) throw new InvalidOperationException(nameof(StateStore) + " could not get local root index");
            if (LocalRootIndex < state_root.Index && state_root.Index < LocalRootIndex + MaxCacheCount)
            {
                cache.Add(state_root.Index, state_root);
                return true;
            }
            using var state_snapshot = Singleton.GetSnapshot();
            StateRoot local_root = state_snapshot.GetStateRoot(state_root.Index);
            if (local_root is null || local_root.Witness != null) return false;
            using var snapshot = Blockchain.Singleton.GetSnapshot();
            if (!state_root.Verify(snapshot)) return false;
            if (local_root.RootHash != state_root.RootHash) return false;
            state_snapshot.AddValidatedStateRoot(state_root);
            state_snapshot.Commit();
            UpdateCurrentSnapshot();
            system.Verifier?.Tell(new VerificationService.ValidatedRootPersisted { Index = state_root.Index });
            return true;
        }

        public void UpdateLocalStateRoot(uint height, List<DataCache.Trackable> change_set)
        {
            using StateSnapshot state_snapshot = Singleton.GetSnapshot();
            foreach (var item in change_set)
            {
                switch (item.State)
                {
                    case TrackState.Added:
                        state_snapshot.Trie.Put(item.Key, item.Item);
                        break;
                    case TrackState.Changed:
                        state_snapshot.Trie.Put(item.Key, item.Item);
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
            state_snapshot.AddLocalStateRoot(state_root);
            state_snapshot.Commit();
            UpdateCurrentSnapshot();
            system.Verifier?.Tell(new VerificationService.BlockPersisted { Index = height });
            CheckValidatedStateRoot(height);
        }

        private void CheckValidatedStateRoot(uint index)
        {
            if (cache.TryGetValue(index, out StateRoot state_root))
            {
                cache.Remove(index);
                Self.Tell(state_root);
            }
        }

        private void UpdateCurrentSnapshot()
        {
            Interlocked.Exchange(ref currentSnapshot, GetSnapshot())?.Dispose();
        }

        protected override void PostStop()
        {
            base.PostStop();
        }

        public static Props Props(NeoSystem core, StatePlugin system, string path)
        {
            return Akka.Actor.Props.Create(() => new StateStore(core, system, path));
        }
    }
}
