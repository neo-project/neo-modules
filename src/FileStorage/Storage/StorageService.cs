using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Akka.Actor;
using Google.Protobuf;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.LocalObjectStorage.Engine;
using Neo.FileStorage.LocalObjectStorage.Shards;
using Neo.FileStorage.Morph.Event;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.Services.Accounting;
using Neo.FileStorage.Services.Container;
using Neo.FileStorage.Services.Control;
using Neo.FileStorage.Services.Control.Service;
using Neo.FileStorage.Services.Netmap;
using Neo.FileStorage.Services.Object.Acl;
using Neo.FileStorage.Services.Reputaion.Service;
using Neo.FileStorage.Services.Session;
using Neo.FileStorage.Storage.Processors;
using Neo.FileStorage.Utils;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.VM;
using Neo.Wallets;

namespace Neo.FileStorage
{
    public sealed partial class StorageService : IDisposable
    {
        public const int ContainerCacheSize = 100;
        public const int ContainerCacheTTLSeconds = 30;
        public const int EACLCacheSize = 100;
        public const int EACLCacheTTLSeconds = 30;
        public ulong CurrentEpoch = 0;
        public API.Netmap.NodeInfo LocalNodeInfo;
        public NetmapStatus NetmapStatus = NetmapStatus.Online;
        public HealthStatus HealthStatus = HealthStatus.Ready;
        private readonly ECDsa key;
        private readonly Client morphClient;
        private readonly NeoSystem system;
        private readonly IActorRef listener;
        public ProtocolSettings ProtocolSettings => system.Settings;
        private Network.Address LocalAddress => Network.Address.FromString(LocalNodeInfo.Address);
        private readonly NetmapProcessor netmapProcessor = new();
        private readonly ContainerProcessor containerProcessor = new();
        private readonly CancellationTokenSource context = new();

        public StorageService(Wallet wallet, NeoSystem side)
        {
            system = side;
            key = wallet.GetAccounts().First().GetKey().PrivateKey.LoadPrivateKey();
            LocalNodeInfo = new()
            {
                Address = "127.0.0.1:8080",
                PublicKey = ByteString.CopyFrom(key.PublicKey()),
            };
            StorageEngine localStorage = new();
            int i = 0;
            foreach (var shardSettings in Settings.Default.LocalStorageSettings.Shards)
            {
                var shard = new Shard(shardSettings, system.ActorSystem.ActorOf(WorkerPool.Props($"Shard{i}", 2)), localStorage.ProcessExpiredTomstones);
                netmapProcessor.AddEpochHandler(p =>
                {
                    if (p is MorphEvent.NewEpochEvent e)
                    {
                        shard.OnNewEpoch(e.EpochNumber);
                    }
                });
                localStorage.AddShard(shard);
                i++;
            }
            localStorage.Open();
            morphClient = new Client
            {
                client = new MorphClient()
                {
                    wallet = wallet,
                    system = system,
                }
            };
            listener = system.ActorSystem.ActorOf(Listener.Props("storage"));
            AccountingServiceImpl accountingService = InitializeAccounting();
            ContainerServiceImpl containerService = InitializeContainer(localStorage);
            ControlServiceImpl controlService = InitializeControl(localStorage);
            NetmapServiceImpl netmapService = InitializeNetmap();
            ObjectServiceImpl objectService = InitializeObject(localStorage);
            ReputationServiceImpl reputationService = InitializeReputation();
            SessionServiceImpl sessionService = InitializeSession();
            listener.Tell(new Listener.BindProcessorEvent { processor = netmapProcessor });
            listener.Tell(new Listener.BindProcessorEvent { processor = containerProcessor });
            listener.Tell(new Listener.Start());
            Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureKestrel(options =>
                    {
                        options.ListenAnyIP(Settings.Default.Port,
                            listenOptions => { listenOptions.Protocols = HttpProtocols.Http2; });
                    });
                    webBuilder.UseStartup(context => new Startup
                    {
                        AccountingService = accountingService,
                        ContainerService = containerService,
                        ControlService = controlService,
                        NetmapService = netmapService,
                        ObjectService = objectService,
                        ReputationService = reputationService,
                    });
                })
                .Build()
                .RunAsync(context.Token);
        }

        public void OnPersisted(Block _1, DataCache _2, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            foreach (var appExec in applicationExecutedList)
            {
                Transaction tx = appExec.Transaction;
                VMState state = appExec.VMState;
                if (tx is null || state != VMState.HALT) continue;
                var notifys = appExec.Notifications;
                if (notifys is null) continue;
                foreach (var notify in notifys)
                {
                    var contract = notify.ScriptHash;
                    if (Settings.Default.Contracts.Contains(contract))
                        listener.Tell(new Listener.NewContractEvent() { notify = notify });
                }
            }
        }

        public void Dispose()
        {
            context.Cancel();
            key?.Dispose();
        }
    }
}
