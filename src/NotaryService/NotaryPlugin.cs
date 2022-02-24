using Akka.Actor;
using Neo.ConsoleService;
using Neo.Cryptography.ECC;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Persistence;
using Neo.Plugins;
using Neo.SmartContract.Native;
using Neo.Wallets;
using System;
using System.Linq;
using Settings = Neo.Plugins.Settings;

namespace Neo.Consensus
{
    public class NotaryPlugin : Plugin
    {
        private NeoSystem System;
        private Wallet wallet;
        private IWalletProvider walletProvider;
        private bool started = false;
        private IActorRef notary;

        public override string Description => "Notary plugin";

        protected override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        protected override void OnSystemLoaded(NeoSystem system)
        {
            if (system.Settings.Network != Settings.Default.Network) return;
            System = system;
            System.ServiceAdded += NeoSystem_ServiceAdded;
        }

        private void NeoSystem_ServiceAdded(object sender, object service)
        {
            if (service is IWalletProvider)
            {
                walletProvider = service as IWalletProvider;
                System.ServiceAdded -= NeoSystem_ServiceAdded;
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

        [ConsoleCommand("notary start", Category = "Notary", Description = "Start notary service")]
        private void OnStart()
        {
            Start(walletProvider?.GetWallet());
        }

        public void Start(Wallet wallet)
        {
            if (started) return;
            if (wallet is null)
            {
                Console.WriteLine("Please open wallet first!");
                return;
            }
            if (!CheckNotaryAvailable(System.StoreView, out ECPoint[] notarynodes))
            {
                Console.WriteLine("The notary service is unavailable");
                return;
            }
            if (!CheckNotaryAccount(wallet, notarynodes))
            {
                Console.WriteLine("There is no notary account in wallet");
                return;
            }
            this.wallet = wallet;
            notary = System.ActorSystem.ActorOf(NotaryService.Props(System, this.wallet));
            System.ActorSystem.EventStream.Subscribe(notary, typeof(Blockchain.PersistCompleted));
            System.ActorSystem.EventStream.Subscribe(notary, typeof(Blockchain.RelayResult));
            notary.Tell(new NotaryService.Start());
            started = true;
        }

        private static bool CheckNotaryAvailable(DataCache snapshot, out ECPoint[] notarynodes)
        {
            uint height = NativeContract.Ledger.CurrentIndex(snapshot) + 1;
            notarynodes = NativeContract.RoleManagement.GetDesignatedByRole(snapshot, Role.Notary, height);
            return notarynodes.Length > 0;
        }

        private static bool CheckNotaryAccount(Wallet wallet, ECPoint[] notarynodes)
        {
            return notarynodes
                .Select(p => wallet.GetAccount(p))
                .Any(p => p is not null && p.HasKey && !p.Lock);
        }
    }
}
