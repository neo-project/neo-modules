using Google.Protobuf;
using Neo.FileStorage.Storage.Core.Object;
using Neo.FileStorage.Storage.Placement;
using Neo.FileStorage.Storage.Services.Object.Put.Target;
using Neo.FileStorage.Placement;
using Neo.FileStorage.Storage.Services.Object.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;


namespace Neo.FileStorage.Storage.Services.Object.Put
{
    public sealed class InnerStream : IDisposable
    {
        public PutService PutService { get; init; }
        public CancellationToken Token { get; init; }
        public ulong MaxObjectSize { get; private set; }
        private ITraverser traverser;
        private IObjectTarget target;

        public void Init(PutInitPrm prm)
        {
            InitTarget(prm);
            target.WriteHeader(prm.Header);
        }

        public void Chunk(ByteString chunk)
        {
            if (target is null)
                throw new InvalidOperationException($"{nameof(PutStream)} target not initilized");
            target.WriteChunk(chunk.ToByteArray());
        }

        public AccessIdentifiers Close()
        {
            if (target is null)
                throw new InvalidOperationException($"{nameof(PutStream)} target not initilized");
            return target.Close();
        }

        public void Dispose()
        {
            target?.Dispose();
        }

        private void InitTarget(PutInitPrm prm)
        {
            if (target is not null)
                throw new InvalidOperationException($"{nameof(PutStream)} init recall");
            PrepareInitPrm(prm);
            MaxObjectSize = PutService.MaxObjectSizeSource.MaxObjectSize();
            if (MaxObjectSize == 0) throw new InvalidOperationException($"{nameof(PutStream)} could not obtain max object size parameter");
            if (prm.Header.Signature is not null)
            {
                target = new ValidationTarget
                {
                    ObjectValidator = new ObjectValidator
                    {
                        DeleteHandler = PutService.ObjectInhumer,
                        EpochSource = PutService.EpochSource,
                    },
                    Next = NewCommonTarget(prm),
                    MaxObjectSize = MaxObjectSize,
                };
                return;
            }
            var key = PutService.KeyStorage.GetKey(prm.SessionToken);
            target = new PayloadSizeLimiterTarget(MaxObjectSize, new FormatTarget
            {
                Key = key,
                SessionToken = prm.SessionToken,
                EpochSource = PutService.EpochSource,
                Next = NewCommonTarget(prm)
            });
        }

        private void PrepareInitPrm(PutInitPrm prm)
        {
            var nm = PutService.NetmapSource.GetNetMapByDiff(0);
            if (nm is null) throw new InvalidOperationException("could not get netmap");
            var container = PutService.ContainerSoruce.GetContainer(prm.Header.ContainerId);
            if (container is null) throw new InvalidOperationException("could not get container");
            var builder = new NetworkMapBuilder(nm);
            if (prm.Local)
            {
                traverser = new Traverser(new LocalPlacementBuilder(builder, PutService.LocalInfo.Addresses), container.PlacementPolicy, prm.Header.Address, 1);
                return;
            }
            traverser = new Traverser(builder, container.PlacementPolicy, prm.Header.Address);
        }

        private DistributeTarget NewCommonTarget(PutInitPrm prm)
        {
            Func<List<Network.Address>, bool> relay = null;
            if (prm.Relay is not null)
            {
                relay = addresses =>
                {
                    if (PutService.LocalInfo.Addresses.Intersect(addresses).Any())
                        return false;
                    var c = PutService.ClientCache.Get(addresses);
                    return prm.Relay(c).Result;
                };
            }
            return new DistributeTarget
            {
                Traverser = traverser,
                Relay = relay,
                ObjectValidator = new ObjectValidator
                {
                    DeleteHandler = PutService.ObjectInhumer,
                    EpochSource = PutService.EpochSource,
                },
                NodeTargetInitializer = addresses =>
                {
                    if (PutService.LocalInfo.Addresses.Intersect(addresses).Any())
                        return new LocalTarget
                        {
                            LocalObjectStore = PutService.LocalObjectStore,
                        };
                    return new RemoteTarget
                    {
                        Token = Token,
                        KeyStorage = PutService.KeyStorage,
                        Prm = prm,
                        Addresses = addresses,
                        PutClientCache = PutService.ClientCache,
                    };
                }
            };
        }
    }
}
