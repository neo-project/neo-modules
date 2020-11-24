
using Akka.Actor;
using Neo;
using Neo.Plugins.StateService.StateStorage;
using Neo.Plugins.StateService.Validation;
using Neo.Wallets;
using Neo.Wallets.NEP6;
using System;
using System.IO;

namespace Neo.Plugins.StateService
{
    public partial class StatePlugin : Plugin
    {
        public ActorSystem ActorSystem { get; } = ActorSystem.Create(nameof(StatePlugin));
        public IActorRef Store { get; }
        public IActorRef Validation { get; }
        public override string Name => "StateService";
        public override string Description => "Enables MPT for the node";

        Wallet wallet;

        public StatePlugin()
        {
            RpcServerPlugin.RegisterMethods(this);
            Store = ActorSystem.ActorOf(StateStore.Props(this, System, Settings.Default.Path));
            if (Settings.Default.StartValidate)
            {
                OpenWallet(Settings.Default.Wallet, Settings.Default.Password);
                if (wallet != null)
                    Validation = ActorSystem.ActorOf(ValidationService.Props(this, wallet));
            }
        }

        protected override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        public override void Dispose()
        {
            base.Dispose();
            if (Validation != null) System.EnsureStoped(Validation);
            System.EnsureStoped(Store);
            ActorSystem.Dispose();
            ActorSystem.WhenTerminated.Wait();
        }

        private void OpenWallet(string path, string password)
        {
            if (!File.Exists(path))
            {
                Log("Wallet not exits");
                return;
            }
            if (global::System.IO.Path.GetExtension(path).ToLowerInvariant() != ".json")
            {
                Log("Invalid wallet format");
                return;
            }
            try
            {
                NEP6Wallet nep6wallet = new NEP6Wallet(path);
                nep6wallet.Unlock(password);
                wallet = nep6wallet;
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                Log("Unlock wallet failed");
                return;
            }
            catch (Exception e)
            {
                Log(e.ToString());
                return;
            }
        }
    }
}
