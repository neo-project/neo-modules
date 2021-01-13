using Akka.Actor;
using Neo.IO.Caching;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins.StateService.Storage;
using System.Collections.Generic;
using System.Linq;
using static Neo.Ledger.Blockchain;

namespace Neo.Plugins.StateService
{
    public class StatePlugin : Plugin, IPersistencePlugin
    {
        public const string StatePayloadCategory = "StateService";
        public ActorSystem ActorSystem { get; } = ActorSystem.Create(nameof(StatePlugin));
        public IActorRef Store { get; }
        public override string Name => "StateService";
        public override string Description => "Enables MPT for the node";

        public StatePlugin()
        {
            Store = ActorSystem.ActorOf(StateStore.Props(this, System, Settings.Default.Path));
        }

        protected override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        public override void Dispose()
        {
            base.Dispose();
            System.EnsureStoped(Store);
            ActorSystem.Dispose();
            ActorSystem.WhenTerminated.Wait();
        }

        void IPersistencePlugin.OnPersist(Block block, StoreView snapshot, IReadOnlyList<ApplicationExecuted> applicationExecutedList)
        {
            List<StateStore.Item> changes = new List<StateStore.Item>();
            foreach (var item in snapshot.Storages.GetChangeSet().Where(p => p.State != TrackState.None))
            {
                changes.Add(new StateStore.Item
                {
                    State = item.State,
                    Key = item.Key,
                    Value = item.Item,
                });
            }
            Store.Tell(new StateStore.StorageChanges
            {
                Height = block.Index,
                ChangeSet = changes,
            });
        }
    }
}
