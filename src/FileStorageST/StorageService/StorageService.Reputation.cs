using Akka.Actor;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.Cache;
using Neo.FileStorage.Invoker.Morph;
using Neo.FileStorage.Listen;
using Neo.FileStorage.Listen.Event.Morph;
using Neo.FileStorage.Reputation;
using Neo.FileStorage.Storage.Services.Reputaion.Common;
using Neo.FileStorage.Storage.Services.Reputaion.Common.Route;
using Neo.FileStorage.Storage.Services.Reputaion.EigenTrust;
using Neo.FileStorage.Storage.Services.Reputaion.EigenTrust.Calculate;
using Neo.FileStorage.Storage.Services.Reputaion.EigenTrust.Route;
using Neo.FileStorage.Storage.Services.Reputaion.EigenTrust.Storage.Consumers;
using Neo.FileStorage.Storage.Services.Reputaion.EigenTrust.Storage.Daughters;
using Neo.FileStorage.Storage.Services.Reputaion.Local;
using Neo.FileStorage.Storage.Services.Reputaion.Local.Routes;
using Neo.FileStorage.Storage.Services.Reputaion.Local.Storage;
using Neo.FileStorage.Storage.Services.Reputaion.Service;
using Neo.FileStorage.Utils;
using System;

namespace Neo.FileStorage.Storage
{
    public sealed partial class StorageService : IDisposable
    {
        private BlockTimer eigenTrustTimer;
        private readonly TrustStorage trustStorage = new();

        private ReputationServiceImpl InitializeReputation()
        {
            IActorRef workPool = system.ActorSystem.ActorOf(WorkerPool.Props("Reputation", Calculator.DefaultWorkPoolSize));
            DaughtersStorage daughterStorage = new();
            ConsumersStorage consumersStorage = new();
            ConsumerStorageWriterProvider consumerStorageWriterProvider = new()
            {
                Storage = consumersStorage
            };
            DaughterStorageWriterProvider daughterStorageWriterProvider = new()
            {
                Storage = daughterStorage
            };
            LocalTrustStorage localTrustStorage = new()
            {
                TrustStorage = trustStorage,
                NetmapCache = netmapCache,
                LocalKey = key.PublicKey()
            };
            ManagerBuilder managerBuilder = new()
            {
                NetmapSource = netmapCache
            };
            LocalRouteBuilder localRouteBuilder = new()
            {
                ManagerBuilder = managerBuilder
            };
            IntermediateRouteBuilder intermediateRouteBuilder = new()
            {
                ManagerBuilder = managerBuilder
            };
            ClientCache clientCache = new();
            RemoteTrustProvider remoteLocalTrustProvider = new()
            {
                LocalInfoSource = this,
                DeadEndProvider = daughterStorageWriterProvider,
                ClientCache = clientCache,
                RemoteProvider = new Services.Reputaion.Local.Remote.RemoteProvider
                {
                    Key = key
                }
            };
            RemoteTrustProvider remoteIntermediateTrustProvider = new()
            {
                LocalInfoSource = this,
                DeadEndProvider = consumerStorageWriterProvider,
                ClientCache = clientCache,
                RemoteProvider = new Services.Reputaion.EigenTrust.Remote.RemoteProvider
                {
                    Key = key
                }
            };
            Router localTrustRouter = new()
            {
                LocalNodeInfoSource = this,
                RemoteWriterProvider = remoteLocalTrustProvider,
                RouteBuilder = localRouteBuilder
            };
            Router intermediateTrustRouter = new()
            {
                LocalNodeInfoSource = this,
                RemoteWriterProvider = remoteIntermediateTrustProvider,
                RouteBuilder = intermediateRouteBuilder
            };
            Calculator eigenTrustCalculator = new()
            {
                MorphInvoker = morphInvoker,
                InitialTrustSource = new()
                {
                    NetmapCache = netmapCache
                },
                IntermediateValueTarget = intermediateTrustRouter,
                WorkerPool = workPool,
                FinalWriteTarget = new()
                {
                    PrivateKey = key,
                    PublicKey = key.PublicKey(),
                    MorphInvoker = morphInvoker
                },
                DaughterTrustSource = new()
                {
                    DaughterStorage = daughterStorage,
                    ConsumerStorage = consumersStorage
                }
            };
            Services.Reputaion.EigenTrust.Control.Controller eigenTrustController = new(morphInvoker, eigenTrustCalculator, workPool);
            Services.Reputaion.Local.Control.Controller localTrustController = new()
            {
                LocalTrustStorage = localTrustStorage,
                Router = localTrustRouter
            };
            netmapProcessor.AddEpochHandler(p =>
            {
                if (p is NewEpochEvent e)
                {
                    localTrustController.Report(e.EpochNumber - 1);
                }
            });
            EigenTrustDuration duration = new() { MorphInvoker = morphInvoker };
            eigenTrustTimer = new(duration.Value, () =>
            {
                ulong epoch = CurrentEpoch;
                eigenTrustController.Continue(epoch - 1);
            });
            blockTimers.Add(eigenTrustTimer);
            return new ReputationServiceImpl
            {
                SignService = new()
                {
                    Key = key,
                    ResponseService = new()
                    {
                        EpochSource = this,
                        ReputationService = new()
                        {
                            LocalInfoSource = this,
                            LocalRouter = localTrustRouter,
                            IntermediateRouter = intermediateTrustRouter,
                            RouteBuilder = localRouteBuilder
                        }
                    }
                }
            };
        }

        private class EigenTrustDuration
        {
            public MorphInvoker MorphInvoker { get; init; }
            private uint value = 0;

            public uint Value()
            {
                lock (this)
                {
                    if (value == 0)
                        UpdateInternal();
                    return value;
                }
            }

            public void Update()
            {
                lock (this)
                {
                    UpdateInternal();
                }
            }

            public void UpdateInternal()
            {
                try
                {
                    var amount = MorphInvoker.EigenTrustIterations();
                    var duration = MorphInvoker.EpochDuration();
                    value = (uint)(duration / amount);
                }
                catch
                {
                    return;
                }
            }
        }
    }
}
