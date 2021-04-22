using Google.Protobuf;
using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Container;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.Services.Container.Announcement;
using FSContainer = Neo.FileStorage.API.Container.Container;

namespace Neo.FileStorage.Services.Container
{
    public class ContainerService
    {
        public Client MorphClient { get; init; }
        public UsedSpaceService UsedSpaceService { get; init; }

        public AnnounceUsedSpaceResponse AnnounceUsedSpace(AnnounceUsedSpaceRequest request)
        {
            return UsedSpaceService.AnnounceUsedSpace(request);
        }

        public DeleteResponse Delete(DeleteRequest request)
        {
            byte[] sig = request.Body.Signature.Sign.ToByteArray();
            MorphClient.InvokeDelete(request.Body.ContainerId, sig);
            var resp = new DeleteResponse
            {
                Body = new DeleteResponse.Types.Body { }
            };
            return resp;
        }

        public GetResponse Get(GetRequest request)
        {
            var container = MorphContractInvoker.InvokeGetContainer(MorphClient, request.Body.ContainerId);
            var resp = new GetResponse
            {
                Body = new GetResponse.Types.Body
                {
                    Container = container,
                }
            };
            return resp;
        }

        public GetExtendedACLResponse GetExtendedACL(GetExtendedACLRequest request)
        {
            var result = MorphContractInvoker.InvokeGetEACL(MorphClient, request.Body.ContainerId);
            var resp = new GetExtendedACLResponse
            {
                Body = new GetExtendedACLResponse.Types.Body
                {
                    Eacl = result.Table,
                    Signature = result.Signature,
                }
            };
            return resp;
        }

        public ListResponse List(ListRequest request)
        {
            var containers = MorphContractInvoker.InvokeGetContainerList(MorphClient, request.Body.OwnerId);
            var resp = new ListResponse { Body = new ListResponse.Types.Body { } };
            resp.Body.ContainerIds.AddRange(containers);
            return resp;
        }

        public PutResponse Put(PutRequest request)
        {
            FSContainer container = request.Body.Container;
            byte[] sig = request.Body.Signature.Sign.ToByteArray();
            byte[] public_key = request.Body.Signature.Key.ToByteArray();
            MorphClient.InvokePut(container, sig, public_key);
            var resp = new PutResponse
            {
                Body = new PutResponse.Types.Body
                {
                    ContainerId = container.CalCulateAndGetId,
                }
            };
            return resp;
        }

        public SetExtendedACLResponse SetExtendedACL(SetExtendedACLRequest request)
        {
            byte[] sig = request.Body.Signature.Sign.ToByteArray();
            MorphClient.InvokeSetEACL(request.Body.Eacl, sig);
            var resp = new SetExtendedACLResponse
            {
                Body = new SetExtendedACLResponse.Types.Body { }
            };
            return resp;
        }
    }
}
