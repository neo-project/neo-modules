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
        public override string Name => "StateService";
        public override string Description => "Enables MPT for the node";

        private IActorRef store;

        protected override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        protected override void OnPluginsLoaded()
        {
            store = System.ActorSystem.ActorOf(StateStore.Props(System, Settings.Default.Path));
        }

        public override void Dispose()
        {
            base.Dispose();
            System.EnsureStoped(store);
        }

        void IPersistencePlugin.OnPersist(Block block, StoreView snapshot, IReadOnlyList<ApplicationExecuted> applicationExecutedList)
        {
            StateStore.Singleton.UpdateLocalStateRoot(block.Index, snapshot.Storages.GetChangeSet().Where(p => p.State != TrackState.None).ToList());
        }
    }
}
