// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Consensus.DBFT is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Collections.Generic;
using System;
using System.IO;
using System.Security.Cryptography;
using Akka.Actor;
using Neo.ConsoleService;
using System.Text.Json;
using Neo.Ledger;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins;
using Neo.Wallets;

namespace Neo.Consensus
{
    public class DBFTPlugin : Plugin
    {
        private IWalletProvider walletProvider;
        private IActorRef consensus;
        private bool started = false;
        private NeoSystem neoSystem;
        private Settings settings;

        public override string Description => "Consensus plugin with dBFT algorithm.";

        public DBFTPlugin()
        {
            RemoteNode.MessageReceived += RemoteNode_MessageReceived;
            Blockchain.Committing += OnCommitting;
        }

        public DBFTPlugin(Settings settings) : this()
        {
            this.settings = settings;
        }

        public override void Dispose()
        {
            RemoteNode.MessageReceived -= RemoteNode_MessageReceived;
        }

        protected override void Configure()
        {
            settings ??= new Settings(GetConfiguration());
        }

        protected override void OnSystemLoaded(NeoSystem system)
        {
            if (system.Settings.Network != settings.Network) return;
            neoSystem = system;
            neoSystem.ServiceAdded += NeoSystem_ServiceAdded;
        }

        private void NeoSystem_ServiceAdded(object sender, object service)
        {
            if (service is not IWalletProvider provider) return;
            walletProvider = provider;
            neoSystem.ServiceAdded -= NeoSystem_ServiceAdded;
            if (settings.AutoStart)
            {
                walletProvider.WalletChanged += WalletProvider_WalletChanged;
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
            consensus = neoSystem.ActorSystem.ActorOf(ConsensusService.Props(neoSystem, settings, wallet));
            consensus.Tell(new ConsensusService.Start());
        }

        private bool RemoteNode_MessageReceived(NeoSystem system, Message message)
        {
            if (message.Command == MessageCommand.Transaction)
            {
                Transaction tx = (Transaction)message.Payload;
                if (tx.SystemFee > settings.MaxBlockSystemFee)
                    return false;
                consensus?.Tell(tx);
            }
            return true;
        }

        private static void OnCommitting(NeoSystem system, Block block, DataCache snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            var serializedData = JsonSerializer.SerializeToUtf8Bytes(applicationExecutedList);
            var hash = ComputeSha256Hash(serializedData);
            File.WriteAllText("prestatehash", hash);
        }

        private static string ComputeSha256Hash(byte[] rawData)
        {
            var bytes = SHA256.HashData(rawData);
            return BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }
    }
}
