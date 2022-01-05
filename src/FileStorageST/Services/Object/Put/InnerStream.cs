using Google.Protobuf;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Storage.Core.Object;
using Neo.FileStorage.Storage.Placement;
using Neo.FileStorage.Storage.Services.Object.Put.Target;
using Neo.FileStorage.Placement;
using Neo.FileStorage.Storage.Services.Object.Util;
using System;
using System.Linq;
using System.Threading;
using static Neo.FileStorage.Network.Helper;

namespace Neo.FileStorage.Storage.Services.Object.Put
{
    public sealed class InnerStream : IDisposable
    {
        public PutService PutService { get; init; }
        public CancellationToken Cancellation { get; init; }
        public ulong MaxObjectSize { get; private set; }
        private Func<ITraverser> traverserInitializer;
        private IObjectTarget target;

        public void Init(PutInitPrm prm)
        {
            InitTarget(prm);
            target.WriteHeader(prm.Header);
        }

        public void Chunk(ByteString chunk)
        {
            if (target is null)
                throw new InvalidOperationException($"{nameof(InnerStream)} target isn't initilized");
            target.WriteChunk(chunk.ToByteArray());
        }

        public AccessIdentifiers Close()
        {
            if (target is null)
                throw new InvalidOperationException($"{nameof(InnerStream)} target isn't initilized");
            return target.Close();
        }

        public void Dispose()
        {
            target?.Dispose();
        }

        private void InitTarget(PutInitPrm prm)
        {
            if (target is not null)
                throw new InvalidOperationException($"{nameof(InnerStream)} init recall");
            PrepareInitPrm(prm);
            MaxObjectSize = PutService.MaxObjectSizeSource.MaxObjectSize();
            if (MaxObjectSize == 0) throw new InvalidOperationException($"{nameof(InnerStream)} could not obtain max object size parameter");
            if (prm.Header.Signature is not null)
            {
                target = new ValidationTarget
                {
                    ObjectValidator = new ObjectValidator
                    {
                        Inhumer = PutService.ObjectInhumer,
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
            }, Cancellation);
        }

        private void PrepareInitPrm(PutInitPrm prm)
        {
            var nm = PutService.NetmapSource.GetNetMapByDiff(0);
            if (nm is null) throw new InvalidOperationException("could not get netmap");
            var container = PutService.ContainerSoruce.GetContainer(prm.Header.ContainerId)?.Container;
            if (container is null) throw new InvalidOperationException("could not get container");
            var builder = new NetworkMapBuilder(nm);
            if (prm.Local)
            {
                traverserInitializer = () => new Traverser(new LocalPlacementBuilder(builder, PutService.LocalInfo), container.PlacementPolicy, prm.Header.Address, 1);
                return;
            }
            traverserInitializer = () => new Traverser(builder, container.PlacementPolicy, prm.Header.Address);
        }

        private DistributeTarget NewCommonTarget(PutInitPrm prm)
        {
            Func<Node, bool> relay = null;
            if (prm.Relay is not null)
            {
                relay = node =>
                {
                    var c = PutService.ClientCache.Get(node.Info);
                    return prm.Relay(c).Result;
                };
            }
            return new DistributeTarget
            {
                Cancellation = Cancellation,
                LocalInfo = PutService.LocalInfo,
                TraverserInitializer = traverserInitializer,
                Relay = relay,
                ObjectValidator = new ObjectValidator
                {
                    Inhumer = PutService.ObjectInhumer,
                    EpochSource = PutService.EpochSource,
                },
                LocalPool = PutService.LocalPool,
                RemotePool = PutService.RemotePool,
                NodeTargetInitializer = (node, token) =>
                {
                    if (PutService.LocalInfo.PublicKey.SequenceEqual(node.PublicKey))
                    {
                        return new LocalTarget
                        {
                            LocalObjectStore = PutService.LocalObjectStore,
                        };
                    }
                    return new RemoteTarget
                    {
                        Cancellation = token,
                        KeyStorage = PutService.KeyStorage,
                        Prm = prm,
                        Node = node.Info,
                        PutClientCache = PutService.ClientCache,
                    };
                }
            };
        }
    }
}
