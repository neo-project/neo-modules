using Neo.Plugins;
using System;
using Neo.Wallets;
using Neo;

namespace FileStorageCLI
{
    public partial class CommandsPlugin : Plugin
    {
        private IWalletProvider walletProvider;
        private Wallet currentWallet;
        private NeoSystem System;

        protected override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        protected override void OnSystemLoaded(NeoSystem system)
        {
            System = system;
            System.ServiceAdded += NeoSystem_ServiceAdded;
        }

        private void NeoSystem_ServiceAdded(object sender, object service)
        {
            if (service is IWalletProvider)
            {
                walletProvider = service as IWalletProvider;
                System.ServiceAdded -= NeoSystem_ServiceAdded;
                walletProvider.WalletChanged += WalletProvider_WalletChanged;
                currentWallet = walletProvider.GetWallet();
            }
        }

        private void WalletProvider_WalletChanged(object sender, Wallet wallet)
        {
            walletProvider.WalletChanged -= WalletProvider_WalletChanged;
            currentWallet = walletProvider.GetWallet();
        }

        private bool NoWallet()
        {
            if (currentWallet != null) return false;
            Console.WriteLine("You have to open the wallet first.");
            return true;
        }

        private bool CheckAccount(UInt160 account)
        {
            if (!currentWallet.Contains(account))
            {
                Console.WriteLine("The specified account does not exist");
                return false;
            }
            return true;
        }
    }
}
