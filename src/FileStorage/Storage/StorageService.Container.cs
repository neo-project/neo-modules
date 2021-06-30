using System;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.Cache;
using Neo.FileStorage.LocalObjectStorage.Engine;
using Neo.FileStorage.Morph.Event;
using Neo.FileStorage.Services.Container;
using Neo.FileStorage.Services.Container.Announcement;
using Neo.FileStorage.Services.Container.Announcement.Control;
using Neo.FileStorage.Services.Container.Announcement.Route;
using Neo.FileStorage.Services.Container.Announcement.Storage;

namespace Neo.FileStorage
{
    public sealed partial class StorageService : IDisposable
    {
        private ContainerServiceImpl InitializeContainer(StorageEngine localStorage)
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
                        LocalAddress = LocalAddress,
                        ClientCache = new ClientCache(),
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
            containerProcessor.AddStartEstimateContainerParser(MorphEvent.StartEstimationEvent.ParseStartEstimationEvent);
            containerProcessor.AddStartEstimateHandler(p =>
            {
                if (p is MorphEvent.StartEstimationEvent e)
                {
                    controller.Start(e.Epoch);
                }
            });
            containerProcessor.AddStopEstimateContainerParser(MorphEvent.StopEstimationEvent.ParseStopEstimationEvent);
            containerProcessor.AddStopEstimateHandler(p =>
            {
                if (p is MorphEvent.StopEstimationEvent e)
                {
                    controller.Stop(e.Epoch);
                }
            });
            var loadPlacementBuilder = new LoadPlacementBuilder
            {
                MorphClient = morphClient,
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
                            MorphClient = morphClient,
                            UsedSpaceService = new()
                            {
                                Key = key,
                                LocalNodeInfo = LocalNodeInfo,
                                Router = new()
                                {
                                    RemoteProvider = new()
                                    {
                                        Key = key,
                                        LocalAddress = LocalAddress,
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
