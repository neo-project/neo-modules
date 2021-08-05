using System;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.Cache;
using Neo.FileStorage.Listen.Event.Morph;
using Neo.FileStorage.Storage.Services.Container;
using Neo.FileStorage.Storage.Services.Container.Announcement;
using Neo.FileStorage.Storage.Services.Container.Announcement.Control;
using Neo.FileStorage.Storage.Services.Container.Announcement.Route;
using Neo.FileStorage.Storage.Services.Container.Announcement.Storage;

namespace Neo.FileStorage.Storage
{
    public sealed partial class StorageService : IDisposable
    {
        private ContainerServiceImpl InitializeContainer()
        {
            AnnouncementStorage loadAccumulator = new();
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
                        LocalAddresses = LocalAddresses,
                        ClientCache = new ClientCache(),
                        DeadEndProvider = new SimpleProvider(loadAccumulator),
                    },
                    RouteBuilder = new()
                    {
                        PlacementBuilder = new()
                        {
                            MorphInvoker = morphInvoker,
                        }
                    }
                },
                ResultReceiver = new SimpleProvider(new MorphLoadWriter
                {
                    PublicKey = key.PublicKey(),
                    MorphInvoker = morphInvoker,
                })
            };
            containerProcessor.AddStartEstimateContainerParser(StartEstimationEvent.ParseStartEstimationEvent);
            containerProcessor.AddStartEstimateHandler(p =>
            {
                if (p is StartEstimationEvent e)
                {
                    controller.Start(e.Epoch);
                }
            });
            containerProcessor.AddStopEstimateContainerParser(StopEstimationEvent.ParseStopEstimationEvent);
            containerProcessor.AddStopEstimateHandler(p =>
            {
                if (p is StopEstimationEvent e)
                {
                    controller.Stop(e.Epoch);
                }
            });
            var loadPlacementBuilder = new LoadPlacementBuilder
            {
                MorphInvoker = morphInvoker,
            };
            return new ContainerServiceImpl
            {
                SignService = new()
                {
                    Key = key,
                    ResponseService = new()
                    {
                        StorageNode = this,
                        ContainerService = new()
                        {
                            MorphInvoker = morphInvoker,
                            UsedSpaceService = new()
                            {
                                Key = key,
                                LocalNodeInfo = LocalNodeInfo,
                                Router = new()
                                {
                                    RemoteProvider = new()
                                    {
                                        Key = key,
                                        LocalAddresses = LocalAddresses,
                                        ClientCache = new ClientCache(),
                                        DeadEndProvider = new SimpleProvider(loadAccumulator),
                                    },
                                    RouteBuilder = new()
                                    {
                                        PlacementBuilder = loadPlacementBuilder
                                    },
                                    LocalNodeInfo = LocalNodeInfo,
                                },
                                Loadbuilder = loadPlacementBuilder,
                                RouteBuilder = new()
                                {
                                    PlacementBuilder = loadPlacementBuilder,
                                }
                            }
                        }
                    }
                }
            };
        }
    }
}
