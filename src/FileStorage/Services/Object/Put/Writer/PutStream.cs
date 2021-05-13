using Google.Protobuf;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Session;
using Neo.FileStorage.Core.Object;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.Services.Object.Util;
using Neo.FileStorage.Services.ObjectManager.Placement;
using Neo.FileStorage.Services.ObjectManager.Transformer;
using System;
using System.Threading;

namespace Neo.FileStorage.Services.Object.Put.Writer
{
    public class PutStream : IRequestStream
    {
        public PutService PutService { get; init; }
        public CancellationToken Cancellation;

        private Traverser traverser;
        private IObjectTarget target;

        public void Send(IRequest request)
        {
            if (request is not PutRequest putRequest)
                throw new InvalidOperationException($"{nameof(PutStream)} invalid object put request");
            switch (putRequest.Body.ObjectPartCase)
            {
                case PutRequest.Types.Body.ObjectPartOneofCase.Init:
                    var init_prm = PutService.ToInitPrm(putRequest);
                    Init(init_prm);
                    break;
                case PutRequest.Types.Body.ObjectPartOneofCase.Chunk:
                    Chunk(putRequest.Body.Chunk);
                    break;
                default:
                    throw new InvalidOperationException($"{nameof(PutStream)} invalid object put request");
            }
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

        public void Init(PutInitPrm prm)
        {
            InitTarget(prm);
            target.WriteHeader(prm.Header);
        }

        private void InitTarget(PutInitPrm prm)
        {
            if (target is not null)
                throw new Exception($"{nameof(PutStream)} init recall");
            PrepareInitPrm(prm);
            if (prm.Header.Signature is not null)
            {
                target = new ValidatingTarget
                {
                    ObjectValidator = new ObjectValidator(PutService.ObjectInhumer, PutService.MorphClient),
                    Next = new DistributeTarget
                    {
                        LocalAddress = PutService.LocalAddress,
                        Traverser = traverser,
                        ObjectValidator = new ObjectValidator(PutService.ObjectInhumer, PutService.MorphClient),
                        NodeTargetInitializer = address =>
                        {
                            if (address == PutService.LocalAddress)
                                return new LocalTarget
                                {
                                    LocalStorage = PutService.LocalStorage,
                                };
                            return new RemoteTarget
                            {
                                Cancellation = Cancellation,
                                KeyStorage = PutService.KeyStorage,
                                Prm = prm,
                                Address = address,
                                ClientCache = PutService.ClientCache,
                            };
                        }
                    },
                };
                return;
            }
            var key = PutService.KeyStorage.GetKey(prm.SessionToken);
            var max = PutService.MorphClient.MaxObjectSize();
            if (max == 0) throw new InvalidOperationException($"{nameof(PutStream)} could not obtain max object size parameter");
            target = new PayloadSizeLimiterTarget(max, new FormatTarget
            {
                Key = key,
                SessionToken = prm.SessionToken,
                MorphClient = PutService.MorphClient,
                Next = new DistributeTarget
                {
                    LocalAddress = PutService.LocalAddress,
                    Traverser = traverser,
                    ObjectValidator = new ObjectValidator(PutService.ObjectInhumer, PutService.MorphClient),
                    NodeTargetInitializer = address =>
                    {
                        if (address == PutService.LocalAddress)
                            return new LocalTarget
                            {
                                LocalStorage = PutService.LocalStorage,
                            };
                        return new RemoteTarget
                        {
                            Cancellation = Cancellation,
                            KeyStorage = PutService.KeyStorage,
                            Prm = prm,
                            Address = address,
                            ClientCache = PutService.ClientCache,
                        };
                    }
                }
            });
        }

        private void PrepareInitPrm(PutInitPrm prm)
        {
            var nm = PutService.MorphClient.InvokeSnapshot(0);
            var container = PutService.MorphClient.InvokeGetContainer(prm.Header.ContainerId);
            var builder = new NetworkMapBuilder(nm);

            if (prm.Local)
            {
                traverser = new Traverser(new LocalPlacementBuilder(builder, PutService.LocalAddress), container.PlacementPolicy, prm.Header.Address, 1);
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
    }
}
