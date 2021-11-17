using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Neo;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Session;
using Neo.Plugins;
using Neo.Wallets;

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

        //check function
        private bool NoWallet()
        {
            if (currentWallet != null) return false;
            Console.WriteLine("You have to open the wallet first.");
            return true;
        }

        private bool CheckAndParseAccount(string paddress, out UInt160 account, out ECDsa key)
        {
            account = null;
            key = null;
            if (NoWallet()) return false;
            try
            {
                account = paddress is null ? currentWallet.GetAccounts().Where(p => !p.WatchOnly).ToArray()[0].ScriptHash : paddress.ToScriptHash(System.Settings.AddressVersion);
            }
            catch (Exception e)
            {
                Console.WriteLine($"The specified format error:{e}");
                return false;
            }
            if (!currentWallet.Contains(account))
            {
                Console.WriteLine("The specified account does not exist");
                return false;
            }
            if (currentWallet.GetAccount(account).WatchOnly)
            {
                Console.WriteLine("The specified account can not be watchonly");
                return false;
            }
            key = currentWallet.GetAccount(account).GetKey().Export().LoadWif();
            return true;
        }

        //internal function
        private Client OnCreateClientInternal(ECDsa key)
        {
            try
            {
                return new Client(key, Settings.Default.Host);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Fs create client fault,error:{e}");
                return null;
            }
        }

        private SessionToken OnCreateSessionInternal(Client client)
        {
            using CancellationTokenSource source = new();
            source.CancelAfter(TimeSpan.FromMinutes(1));
            try
            {
                var session = client.CreateSession(ulong.MaxValue, context: source.Token).Result;
                source.Cancel();
                return session;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Fs create session fault,error:{e}");
                source.Cancel();
                return null;
            }
        }
    }
}
