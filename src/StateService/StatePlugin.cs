
using Akka.Actor;
using Neo.Plugins.StateService.Storage;
using Neo.Wallets;

namespace Neo.Plugins.StateService
{
    public partial class StatePlugin : Plugin
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
    }
}
