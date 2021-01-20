using Akka.Actor;
using Neo.ConsoleService;
using Neo.IO.Caching;
using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins.StateService.Storage;
using Neo.Plugins.StateService.Validation;
using System;
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

        internal IActorRef Store;
        internal IActorRef Validator;

        protected override void Configure()
        {
            Settings.Load(GetConfiguration());
            RpcServerPlugin.RegisterMethods(this);
        }

        protected override void OnPluginsLoaded()
        {
            Store = System.ActorSystem.ActorOf(StateStore.Props(System, this, Settings.Default.Path));
        }

        public override void Dispose()
        {
            base.Dispose();
            System.EnsureStoped(Store);
            if (Validator != null) System.EnsureStoped(Validator);
        }

        void IPersistencePlugin.OnPersist(Block block, StoreView snapshot, IReadOnlyList<ApplicationExecuted> applicationExecutedList)
        {
            StateStore.Singleton.UpdateLocalStateRoot(block.Index, snapshot.Storages.GetChangeSet().Where(p => p.State != TrackState.None).ToList());
        }

        [RpcMethod]
        public JObject VoteStateRoot(JArray _params)
        {
            uint height = uint.Parse(_params[0].AsString());
            int validator_index = int.Parse(_params[1].AsString());
            byte[] sig = _params[2].AsString().HexToBytes();
            Validator?.Tell(new Vote(height, validator_index, sig));
            return true;
        }

        [ConsoleCommand("start validate", Category = "StateService", Description = "Start as a state validator if wallet is open")]
        private void OnStartValidate()
        {
            var wallet = GetService<IWalletProvider>().GetWallet();
            if (wallet is null)
            {
                Console.WriteLine("Please open wallet first!");
                return;
            }
            Validator = System.ActorSystem.ActorOf(ValidationService.Props(System, wallet));
        }
    }
}
