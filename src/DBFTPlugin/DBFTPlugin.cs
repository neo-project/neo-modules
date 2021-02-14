using Akka.Actor;
using Neo.ConsoleService;
using Neo.Network.P2P;
using Neo.Plugins;
using Neo.Wallets;

namespace Neo.Consensus
{
    public class DBFTPlugin : Plugin, IP2PPlugin
    {
        private IActorRef consensus;
        private bool started = false;
        internal NeoSystem System;

        public override string Description => "Consensus plugin with dBFT algorithm.";

        protected override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        protected override void OnSystemLoaded(NeoSystem system)
        {
            if (system.Settings.Magic != Settings.Default.Active) return;
            System = system;
        }

        [ConsoleCommand("start consensus", Category = "Consensus", Description = "Start consensus service (dBFT)")]
        private void OnStart()
        {
            Start(System.GetService<IWalletProvider>().GetWallet());
        }

        public void Start(Wallet wallet)
        {
            if (started) return;
            started = true;
            consensus = System.ActorSystem.ActorOf(ConsensusService.Props(System, System.LocalNode, System.TaskManager, System.Blockchain, System.LoadStore(Settings.Default.RecoveryLogs), wallet));
            consensus.Tell(new ConsensusService.Start());
        }

        bool IP2PPlugin.OnP2PMessage(NeoSystem system, Message message)
        {
            if (message.Command == MessageCommand.Transaction)
                consensus?.Tell(message.Payload);
            return true;
        }
    }
}
