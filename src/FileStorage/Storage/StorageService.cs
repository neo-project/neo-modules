using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Akka.Actor;
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
        public ulong CurrentEpoch;
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

        public StorageService(Wallet wallet, NeoSystem side)
        {
            system = side;
            key = wallet.GetAccounts().First().GetKey().PrivateKey.LoadPrivateKey();
            StorageEngine localStorage = new();
            int i = 0;
            foreach (var shardSettings in Settings.Default.LocalStorageSettings.Shards)
            {
                localStorage.AddShard(new Shard(shardSettings, system.ActorSystem.ActorOf(WorkerPool.Props($"Shard{i}", 2)), localStorage.ProcessExpiredTomstones));
                i++;
            }
            morphClient = new Client
            {
                client = new MorphClient()
                {
                    wallet = wallet,
                    system = system,
                }
            };
            listener = system.ActorSystem.ActorOf(Listener.Props("storage"));
            SessionServiceImpl sessionService = InitializeSession();
            AccountingServiceImpl accountingService = InitializeAccounting();
            ContainerServiceImpl containerService = InitializeContainer(localStorage);
            ControlServiceImpl controlService = InitializeControl(localStorage);
            NetmapServiceImpl netmapService = InitializeNetmap();
            ObjectServiceImpl objectService = InitializeObject(localStorage);
            ReputationServiceImpl reputationServiceImpl = InitializeReputation();
            listener.Tell(new Listener.BindProcessorEvent { processor = netmapProcessor });
            listener.Tell(new Listener.BindProcessorEvent { processor = containerProcessor });
            listener.Tell(new Listener.Start());
            //Start grpc server
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
            key.Dispose();
        }
    }
}
