using Google.Protobuf;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Core.Object;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.Services.Object.Util;
using Neo.FileStorage.Services.ObjectManager.Placement;
using Neo.FileStorage.Services.ObjectManager.Transformer;
using System;
using FSContainer = Neo.FileStorage.API.Container.Container;

namespace Neo.FileStorage.Services.Object.Put.Writer
{
    public class PutStream : IPutRequestStream
    {
        public PutService PutService { get; init; }

        private Traverser traverser;
        private IObjectTarget target;

        public void Send(PutRequest request)
        {
            switch (request.Body.ObjectPartCase)
            {
                case PutRequest.Types.Body.ObjectPartOneofCase.Init:
                    var init_prm = PutService.ToInitPrm(request);
                    Init(init_prm);
                    break;
                case PutRequest.Types.Body.ObjectPartOneofCase.Chunk:
                    Chunk(request.Body.Chunk);
                    break;
                default:
                    throw new InvalidOperationException($"{nameof(PutStream)} invalid object put request");
            }
        }

        public PutResponse Close()
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

        private void Init(PutInitPrm prm)
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
                            return new RemoteTarget();
                        }
                    },
                };
                return;
            }
            var key = PutService.KeyStorage.GetKey(prm.SessionToken);
            var max = GetMaxObjectSize(); //TODO: check 0?
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
                        return new RemoteTarget();
                    }
                }
            });
        }

        private void PrepareInitPrm(PutInitPrm prm)
        {
            var nm = GetLatestNetmap();
            var container = GetContainer(prm.Header.ContainerId);
            var builder = new NetworkMapBuilder(nm);
            traverser = new Traverser()
                .ForContainer(container)
                .ForObjectID(prm.Header.ObjectId)
                .WithBuilder(builder);
            if (prm.Local)
            {
                traverser
                .SuccessAfter(1)
                .WithBuilder(new LocalPlacementBuilder(builder, PutService.LocalAddress));
            }
        }

        private void Chunk(ByteString chunk)
        {
            if (target is null)
                throw new InvalidOperationException($"{nameof(PutStream)} target not initilized");
            target.WriteChunk(chunk.ToByteArray());
        }

        private NetMap GetLatestNetmap()
        {
            return MorphContractInvoker.InvokeSnapshot(PutService.MorphClient, 0);
        }

        private FSContainer GetContainer(ContainerID cid)
        {
            return MorphContractInvoker.InvokeGetContainer(PutService.MorphClient, cid);
        }

        private ulong GetMaxObjectSize()
        {
            return BitConverter.ToUInt64(MorphContractInvoker.InvokeConfig(PutService.MorphClient, MorphContractInvoker.MaxObjectSizeConfig));
        }
    }
}
