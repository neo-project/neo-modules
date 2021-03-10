using Akka.Actor;
using Neo.ConsoleService;
using Neo.Network.P2P;
using Neo.Plugins;
using Neo.Wallets;

namespace Neo.Consensus
{
    public class DBFTPlugin : Plugin, IP2PPlugin
    {
        private IWalletProvider walletProvider;
        private IActorRef consensus;
        private bool started = false;
        private NeoSystem neoSystem;

        public override string Description => "Consensus plugin with dBFT algorithm.";

        protected override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        protected override void OnSystemLoaded(NeoSystem system)
        {
            if (system.Settings.Magic != Settings.Default.Network) return;
            neoSystem = system;
            neoSystem.ServiceAdded += NeoSystem_ServiceAdded;
        }

        private void NeoSystem_ServiceAdded(object sender, object service)
        {
            if (service is IWalletProvider)
            {
                walletProvider = service as IWalletProvider;
                neoSystem.ServiceAdded -= NeoSystem_ServiceAdded;
                if (Settings.Default.AutoStart)
                {
                    walletProvider.WalletChanged += WalletProvider_WalletChanged;
                }
            }
        }

        private void WalletProvider_WalletChanged(object sender, Wallet wallet)
        {
            walletProvider.WalletChanged -= WalletProvider_WalletChanged;
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
            consensus = neoSystem.ActorSystem.ActorOf(ConsensusService.Props(neoSystem, Settings.Default, wallet));
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
