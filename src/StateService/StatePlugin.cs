using Akka.Actor;
using Neo.ConsoleService;
using Neo.IO.Caching;
using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins.StateService.Storage;
using Neo.Plugins.StateService.Verification;
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
        internal IActorRef Verifier;

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
            if (Verifier != null) System.EnsureStoped(Verifier);
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
            byte[] sig = Convert.FromBase64String(_params[2].AsString());
            Verifier?.Tell(new Vote(height, validator_index, sig));
            return true;
        }

        [ConsoleCommand("start verifying state", Category = "StateService", Description = "Start as a state verifier if wallet is open")]
        private void OnStartVerifyingState()
        {
            if (Verifier != null)
            {
                Console.WriteLine("Already started!");
                return;
            }
            var wallet = GetService<IWalletProvider>().GetWallet();
            if (wallet is null)
            {
                Console.WriteLine("Please open wallet first!");
                return;
            }
            Verifier = System.ActorSystem.ActorOf(VerificationService.Props(System, wallet));
        }
    }
}
