using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Session;
using Neo.FileStorage.Storage.Core.Object;
using Neo.FileStorage.Placement;
using Neo.FileStorage.Storage.Services.Object.Util;
using Neo.FileStorage.Storage.Placement;
using Neo.FileStorage.Storage.Services.Object.Put.Target;
using Neo.FileStorage.Storage.Services.Object.Put.Remote;

namespace Neo.FileStorage.Storage.Services.Object.Put
{
    public sealed class PutStream : IRequestStream
    {
        public PutService PutService { get; init; }
        public CancellationToken Cancellation { get; init; }

        private ITraverser traverser;
        private IObjectTarget target;
        private PutRequest init;
        private readonly List<PutRequest> chunks = new();
        private ulong maxObjectSize;
        private bool saveChunks;
        private ulong writenPayloadSize;
        private ulong payloadSize;

        public PutInitPrm ToInitPrm(PutRequest request)
        {
            var prm = PutInitPrm.FromRequest(request);
            prm.Relay = RelayRequest;
            return prm;
        }

        public void Send(IRequest request)
        {
            if (request is not PutRequest putRequest)
                throw new InvalidOperationException($"{nameof(PutStream)} invalid object put request");
            switch (putRequest.Body.ObjectPartCase)
            {
                case PutRequest.Types.Body.ObjectPartOneofCase.Init:
                    var init_prm = ToInitPrm(init);
                    Init(init_prm);
                    saveChunks = init.Body.Init.Signature is not null;
                    if (saveChunks)
                    {
                        payloadSize = init.Body.Init.Header.PayloadLength;
                        if (maxObjectSize < payloadSize)
                            throw new InvalidOperationException("payload size is greater than the limit");
                        init = putRequest;
                    }
                    break;
                case PutRequest.Types.Body.ObjectPartOneofCase.Chunk:
                    if (saveChunks)
                    {
                        writenPayloadSize += (ulong)putRequest.Body.Chunk.Length;
                        if (payloadSize < writenPayloadSize)
                            throw new InvalidOperationException("wrong payload size");
                    }
                    Chunk(putRequest.Body.Chunk);
                    if (saveChunks)
                        chunks.Add(putRequest);
                    break;
                default:
                    throw new InvalidOperationException($"{nameof(PutStream)} invalid object put request");
            }
            if (!saveChunks) return;
            var metaHdr = new RequestMetaHeader();
            var meta = request.MetaHeader;
            metaHdr.Ttl = meta.Ttl - 1;
            metaHdr.Origin = meta;
            request.MetaHeader = metaHdr;
            var key = PutService.KeyStorage.GetKey(meta.SessionToken);
            key.SignRequest(request);
        }

        public IResponse Close()
        {
            if (target is null)
                throw new InvalidOperationException($"{nameof(PutStream)} target not initilized");
            var ids = target.Close();
            var id = ids.Parent ?? ids.Self;
            return new PutResponse
            {
                Body = new PutResponse.Types.Body
                {
                    ObjectId = id,
                }
            };
        }

        public void Dispose()
        {
            target?.Dispose();
        }

        public void Init(PutInitPrm prm)
        {
            InitTarget(prm);
            target.WriteHeader(prm.Header);
        }

        private void InitTarget(PutInitPrm prm)
        {
            if (target is not null)
                throw new InvalidOperationException($"{nameof(PutStream)} init recall");
            PrepareInitPrm(prm);
            maxObjectSize = PutService.MaxObjectSizeSource.MaxObjectSize();
            if (maxObjectSize == 0) throw new InvalidOperationException($"{nameof(PutStream)} could not obtain max object size parameter");
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
                    MaxObjectSize = maxObjectSize,
                };
                return;
            }
            var key = PutService.KeyStorage.GetKey(prm.SessionToken);
            target = new PayloadSizeLimiterTarget(maxObjectSize, new FormatTarget
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
            var container = PutService.ContainerSoruce.GetContainer(prm.Header.ContainerId);
            var builder = new NetworkMapBuilder(nm);

            if (prm.Local)
            {
                traverser = new Traverser(new LocalPlacementBuilder(builder, PutService.LocalInfo.Addresses), container.PlacementPolicy, prm.Header.Address, 1);
                return;
            }
            traverser = new Traverser(builder, container.PlacementPolicy, prm.Header.Address);
        }

        public void Chunk(ByteString chunk)
        {
            if (target is null)
                throw new InvalidOperationException($"{nameof(PutStream)} target not initilized");
            target.WriteChunk(chunk.ToByteArray());
        }

        public async Task RelayRequest(IPutClient client)
        {
            if (init is null) return;
            using var stream = await client.PutObject(init, context: Cancellation);
            foreach (var chunk in chunks)
                await stream.Write(chunk);
            await stream.Close();
        }

        public DistributeTarget NewCommonTarget(PutInitPrm prm)
        {
            Action<List<Network.Address>> relay = null;
            if (prm.Relay is not null)
            {
                relay = addresses =>
                {
                    if (PutService.LocalInfo.Addresses.Intersect(addresses).Any())
                        return;
                    var c = PutService.ClientCache.Get(addresses);
                    prm.Relay(c);
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
                        Token = Cancellation,
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
