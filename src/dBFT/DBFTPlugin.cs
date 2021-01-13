using Akka.Actor;
using Neo.ConsoleService;
using Neo.Network.P2P;
using Neo.Plugins;
using Neo.Wallets;

namespace Neo.Consensus
{
    public class DBFTPlugin : Plugin, IConsensusProvider, IP2PPlugin
    {
        private IActorRef consensus;
        private bool started = false;

        protected override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        [ConsoleCommand("start consensus", Category = "Consensus", Description = "Start consensus service (dBFT)")]
        private void OnStart()
        {
            Start(GetService<IWalletProvider>().GetWallet());
        }

        public void Start(Wallet wallet)
        {
            if (started) return;
            started = true;
            consensus = System.ActorSystem.ActorOf(ConsensusService.Props(System.LocalNode, System.TaskManager, System.Blockchain, System.LoadStore(Settings.Default.RecoveryLogs), wallet));
            consensus.Tell(new ConsensusService.Start());
        }

        bool IP2PPlugin.OnP2PMessage(Message message)
        {
            if (message.Command == MessageCommand.Transaction)
                consensus?.Tell(message.Payload);
            return true;
        }
    }
}
