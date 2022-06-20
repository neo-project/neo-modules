using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.Cache;
using Neo.FileStorage.Listen.Event.Morph;
using Neo.FileStorage.Storage.Services.Container;
using Neo.FileStorage.Storage.Services.Container.Cache;
using Neo.FileStorage.Storage.Services.Container.Announcement;
using Neo.FileStorage.Storage.Services.Container.Announcement.Control;
using Neo.FileStorage.Storage.Services.Container.Announcement.Route;
using Neo.FileStorage.Storage.Services.Container.Announcement.Storage;
using System;
using Neo.FileStorage.Storage.Services.Container.Announcement.Route.Placement;

namespace Neo.FileStorage.Storage
{
    public sealed partial class StorageService : IDisposable
    {
        private ContainerCache containerCache;
        private EACLCache eACLCache;
        private readonly ClientCache clientCache = new();

        private ContainerServiceImpl InitializeContainer()
        {
            AnnouncementStorage loadAccumulator = new();
            containerCache = new(ContainerService.CacheSize, ContainerService.CacheTTL, morphInvoker);
            eACLCache = new(ContainerService.CacheSize, ContainerService.CacheTTL, morphInvoker);
            ContainerListCache containerListCache = new(ContainerService.CacheSize, ContainerService.CacheTTL, morphInvoker);
            LoadPlacementBuilder loadBuilder = new()
            {
                NetmapSource = netmapCache,
                ContainerSoruce = containerCache
            };
            RouteBuilder routeBuilder = new()
            {
                PlacementBuilder = loadBuilder
            };
            LoadRouter loadRouter = new()
            {
                LocalInfo = this,
                RemoteProvider = new RemoteLoadAnnounceProvider
                {
                    Key = key,
                    LocalInfo = this,
                    ClientCache = clientCache,
                    DeadEndProvider = new SimpleWriteProvider(loadAccumulator),
                },
                RouteBuilder = routeBuilder
            };
            Controller controller = new()
            {
                LocalMetrics = new SimpleIterateProvider(new LocalStorageLoad
                {
                    LocalStorage = localStorage
                }),
                AnnouncementAccumulator = new SimpleIterateProvider(loadAccumulator),
                LocalAnnouncementTarget = loadRouter,
                ResultReceiver = new SimpleWriteProvider(new MorphLoadWriter
                {
                    PublicKey = key.PublicKey(),
                    MorphInvoker = morphInvoker
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
            return new ContainerServiceImpl
            {
                SignService = new()
                {
                    Key = key,
                    ResponseService = new()
                    {
                        EpochSource = this,
                        ContainerService = new()
                        {
                            MorphInvoker = morphInvoker,
                            ContainerCache = containerCache,
                            EACLCache = eACLCache,
                            ContainerListCache = containerListCache,
                            UsedSpaceService = new()
                            {
                                Key = key,
                                LocalInfo = this,
                                Router = loadRouter,
                                Loadbuilder = loadBuilder,
                                RouteBuilder = routeBuilder
                            }
                        }
                    }
                }
            };
        }
    }
}
