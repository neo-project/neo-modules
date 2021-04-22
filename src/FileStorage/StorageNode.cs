using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.LocalObjectStorage.Engine;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.Services.Accounting;
using Neo.FileStorage.Services.Container;
using Neo.FileStorage.Services.Container.Announcement;
using Neo.FileStorage.Services.Container.Announcement.Control;
using Neo.FileStorage.Services.Container.Announcement.Route;
using Neo.FileStorage.Services.Container.Announcement.Storage;
using Neo.Wallets;
using System;
using System.Security.Cryptography;
using APIAccountingService = Neo.FileStorage.API.Accounting.AccountingService;
using APIContainerService = Neo.FileStorage.API.Container.ContainerService;

namespace Neo.FileStorage
{
    public sealed class StorageNode : IDisposable
    {
        public ProtocolSettings ProtocolSettings;
        private ECDsa key;
        private Client morphClient;
        private readonly Wallet wallet;
        private readonly NeoSystem system;
        public ulong CurrentEpoch;
        public NodeInfo LocalNodeInfo;

        public ECDsa Key => key;

        public StorageNode()
        {
            StorageEngine localStorage = new();
            morphClient = new Client
            {
                client = new MorphClient()
                {
                    wallet = wallet,
                    system = system,
                }
            };
            var loadAccumulator = new AnnouncementStorage();
            Controller controller = new()
            {
                LocalMetrics = new SimpleProvider(new LocalStorageLoad
                {
                    LocalStorage = localStorage,
                }),
                AnnouncementAccumulator = new SimpleProvider(loadAccumulator),
                LocalAnnouncementTarget = new LoadRouter
                {
                    LocalNodeInfo = LocalNodeInfo,
                    RemoteProvider = new RemoteLoadAnnounceProvider
                    {
                        Key = key,
                        LocalAddress = Network.Address.AddressFromString(LocalNodeInfo.Address),
                        ClientCache = new Network.Cache.ClientCache(),
                        DeadEndProvider = new SimpleProvider(loadAccumulator),
                    },
                    RouteBuilder = new()
                    {
                        PlacementBuilder = new()
                        {
                            MorphClient = morphClient,
                        }
                    }
                },
                ResultReceiver = new SimpleProvider(new MorphLoadWriter
                {
                    PublicKey = key.PublicKey(),
                    MorphClient = morphClient,
                })
            };

            APIAccountingService.BindService(new AccountingServiceImpl
            {
                SignService = new()
                {
                    Key = key,
                    ResponseService = new()
                    {
                        StorageNode = this,
                        AccountingService = new()
                        {
                            MorphClient = morphClient,
                        }
                    }
                }
            });

            APIContainerService.BindService(new ContainerServiceImpl
            {
                SignService = new()
                {
                    Key = key,
                    ResponseService = new()
                    {
                        StorageNode = this,
                        ContainerService = new()
                        {
                            MorphClient = morphClient,
                            UsedSpaceService = new()
                            {
                                Key = key,
                                LocalNodeInfo = LocalNodeInfo,
                                Router = new()
                                {

                                },

                            }
                        }
                    }
                }
            });
        }

        public void Dispose()
        {
            key.Dispose();
        }
    }
}
