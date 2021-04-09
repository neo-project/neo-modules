using Google.Protobuf;
using Grpc.Core;
using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Container;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.Services.Container.Announcement;
using System.Security.Cryptography;
using System.Threading.Tasks;
using FSContainer = Neo.FileStorage.API.Container.Container;

namespace Neo.FileStorage.Services.Container
{
    public class ContainerServiceImpl : ContainerService.ContainerServiceBase
    {
        private readonly IClient morphClient;
        private readonly ECDsa key;
        private readonly UsedSpaceService usedSpaceService;

        public ContainerServiceImpl(ECDsa k, IClient morph)
        {
            morphClient = morph;
            key = k;
        }

        public override Task<AnnounceUsedSpaceResponse> AnnounceUsedSpace(AnnounceUsedSpaceRequest request, ServerCallContext context)
        {
            return usedSpaceService.AnnounceUsedSpace(request, context).ContinueWith(t =>
            {
                key.SignResponse(t.Result);
                return t.Result;
            });
        }

        public override Task<DeleteResponse> Delete(DeleteRequest request, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                byte[] cid = request.Body.ContainerId.ToByteArray();
                byte[] sig = request.Body.Signature.Sign.ToByteArray();
                bool ok = MorphContractInvoker.InvokeDelete(morphClient, new MorphContractInvoker.DeleteArgs { cid = cid, sig = sig });
                var resp = new DeleteResponse
                {
                    Body = new DeleteResponse.Types.Body { }
                };
                key.SignResponse(resp);
                return resp;
            }, context.CancellationToken);
        }

        public override Task<GetResponse> Get(GetRequest request, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                byte[] cid = request.Body.ContainerId.ToByteArray();
                byte[] raw = MorphContractInvoker.InvokeGetContainer(morphClient, cid);
                var resp = new GetResponse
                {
                    Body = new GetResponse.Types.Body
                    {
                        Container = FSContainer.Parser.ParseFrom(raw),
                    }
                };
                key.SignResponse(resp);
                return resp;
            }, context.CancellationToken);
        }

        public override Task<GetExtendedACLResponse> GetExtendedACL(GetExtendedACLRequest request, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                byte[] cid = request.Body.ContainerId.ToByteArray();
                MorphContractInvoker.EACLValues result = MorphContractInvoker.InvokeGetEACL(morphClient, cid);
                var resp = new GetExtendedACLResponse
                {
                    Body = new GetExtendedACLResponse.Types.Body
                    {
                        Eacl = EACLTable.Parser.ParseFrom(result.eacl),
                        Signature = Signature.Parser.ParseFrom(result.sig),
                    }
                };
                key.SignResponse(resp);
                return resp;
            }, context.CancellationToken);
        }

        public override Task<ListResponse> List(ListRequest request, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                byte[] owner = request.Body.OwnerId.Value.ToByteArray();
                byte[][] containers = MorphContractInvoker.InvokeGetContainerList(morphClient, owner);
                var resp = new ListResponse { Body = new ListResponse.Types.Body { } };
                foreach (byte[] c in containers)
                {
                    ContainerID cid = ContainerID.Parser.ParseFrom(c);
                    resp.Body.ContainerIds.Add(cid);
                }
                key.SignResponse(resp);
                return resp;
            }, context.CancellationToken);
        }

        public override Task<PutResponse> Put(PutRequest request, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                FSContainer container = request.Body.Container;
                byte[] sig = request.Body.Signature.Sign.ToByteArray();
                byte[] public_key = request.Body.Signature.Key.ToByteArray();
                bool ok = MorphContractInvoker.InvokePut(morphClient, new MorphContractInvoker.PutArgs { cnr = container.ToByteArray(), sig = sig, publicKey = public_key });
                var resp = new PutResponse
                {
                    Body = new PutResponse.Types.Body
                    {
                        ContainerId = container.CalCulateAndGetId,
                    }
                };
                key.SignResponse(resp);
                return resp;
            }, context.CancellationToken);
        }

        public override Task<SetExtendedACLResponse> SetExtendedACL(SetExtendedACLRequest request, ServerCallContext context)
        {
            return Task.Run(() =>
            {
                byte[] eacl = request.Body.Eacl.ToByteArray();
                byte[] sig = request.Body.Signature.Sign.ToByteArray();
                bool ok = MorphContractInvoker.InvokeSetEACL(morphClient, new MorphContractInvoker.SetEACLArgs { eacl = eacl, sig = sig });
                var resp = new SetExtendedACLResponse
                {
                    Body = new SetExtendedACLResponse.Types.Body { }
                };
                key.SignResponse(resp);
                return resp;
            }, context.CancellationToken);
        }
    }
}
