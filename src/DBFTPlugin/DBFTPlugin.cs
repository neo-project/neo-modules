// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Consensus.DBFT is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Akka.Actor;
using Neo.ConsoleService;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Plugins;
using Neo.Wallets;

namespace Neo.Consensus
{
    public class DBFTPlugin : Plugin
    {
        private IWalletProvider _walletProvider;
        private IActorRef _consensus;
        private bool _started = false;
        private NeoSystem _neoSystem;
        private Settings _settings;

        public override string Description => "Consensus plugin with dBFT algorithm.";

        public DBFTPlugin()
        {
            RemoteNode.MessageReceived += RemoteNode_MessageReceived;
        }

        public DBFTPlugin(Settings settings) : this()
        {
            this._settings = settings;
        }

        public override void Dispose()
        {
            RemoteNode.MessageReceived -= RemoteNode_MessageReceived;
        }

        protected override void Configure()
        {
            _settings ??= new Settings(GetConfiguration());
        }

        protected override void OnSystemLoaded(NeoSystem system)
        {
            if (system.Settings.Network != _settings.Network) return;
            _neoSystem = system;
            _neoSystem.ServiceAdded += NeoSystem_ServiceAdded;
        }

        private void NeoSystem_ServiceAdded(object sender, object service)
        {
            if (service is not IWalletProvider provider) return;
            _walletProvider = provider;
            _neoSystem.ServiceAdded -= NeoSystem_ServiceAdded;
            if (_settings.AutoStart)
            {
                _walletProvider.WalletChanged += WalletProvider_WalletChanged;
            }
        }

        private void WalletProvider_WalletChanged(object sender, Wallet wallet)
        {
            _walletProvider.WalletChanged -= WalletProvider_WalletChanged;
            Start(wallet);
        }

        [ConsoleCommand("start consensus", Category = "Consensus", Description = "Start consensus service (dBFT)")]
        private void OnStart()
        {
            Start(_walletProvider.GetWallet());
        }

        public void Start(Wallet wallet)
        {
            if (_started) return;
            _started = true;
            _consensus = _neoSystem.ActorSystem.ActorOf(ConsensusService.Props(_neoSystem, _settings, wallet));
            _consensus.Tell(new ConsensusService.Start());
        }

        private bool RemoteNode_MessageReceived(NeoSystem system, Message message)
        {
            if (message.Command == MessageCommand.Transaction)
            {
                Transaction tx = (Transaction)message.Payload;
                if (tx.SystemFee > _settings.MaxBlockSystemFee)
                    return false;
                _consensus?.Tell(tx);
            }
            return true;
        }
    }
}
