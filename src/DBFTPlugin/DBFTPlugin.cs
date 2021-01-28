using Akka.Actor;
using Neo.ConsoleService;
using Neo.Network.P2P;
using Neo.Plugins;
using Neo.Wallets;

namespace Neo.Consensus
{
    public class DBFTPlugin : Plugin, IConsensusProvider, IP2PPlugin
    {
        private IWalletProvider walletProvider;
        private IActorRef consensus;
        private bool started = false;

        public override string Description => "Consensus plugin with dBFT algorithm.";

        protected override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        protected override void OnPluginsLoaded()
        {
            walletProvider = GetService<IWalletProvider>();
            if (Settings.Default.AutoStart)
                walletProvider.WalletOpened += WalletProvider_WalletOpened;
        }

        private void WalletProvider_WalletOpened(object sender, Wallet wallet)
        {
            walletProvider.WalletOpened -= WalletProvider_WalletOpened;
            Start(wallet);
        }

        [ConsoleCommand("start consensus", Category = "Consensus", Description = "Start consensus service (dBFT)")]
        private void OnStart()
        {
            Start(walletProvider.GetWallet());
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
