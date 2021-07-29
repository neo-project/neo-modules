using Neo.Plugins;
using System;
using Neo.Wallets;
using Neo;
using System.Linq;
using Neo.FileStorage.API.Cryptography;
using System.Security.Cryptography;
using Neo.FileStorage.API.Refs;

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
            currentWallet = wallet;
        }

        private bool NoWallet()
        {
            if (currentWallet != null) return false;
            Console.WriteLine("You have to open the wallet first.");
            return true;
        }

        private bool CheckAndParseAccount(string paccount, out UInt160 account, out ECDsa key)
        {
            account = null;
            key = null;
            if (NoWallet()) return false;
            account = paccount is null ? currentWallet.GetAccounts().Where(p => !p.WatchOnly).ToArray()[0].ScriptHash : UInt160.Parse(paccount);
            if (!currentWallet.Contains(account))
            {
                Console.WriteLine("The specified account does not exist");
                return false;
            }
            if (currentWallet.GetAccount(account).WatchOnly) {
                Console.WriteLine("The specified account can not be WatchOnly");
                return false;
            }
            key = currentWallet.GetAccount(account).GetKey().Export().LoadWif();
            return true;
        }
    }
}
