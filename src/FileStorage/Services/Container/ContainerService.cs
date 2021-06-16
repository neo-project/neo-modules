using System.Threading;
using Google.Protobuf;
using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Container;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.Session;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.Services.Container.Announcement;
using FSContainer = Neo.FileStorage.API.Container.Container;

namespace Neo.FileStorage.Services.Container
{
    public class ContainerService
    {
        public Client MorphClient { get; init; }
        public UsedSpaceService UsedSpaceService { get; init; }

        public AnnounceUsedSpaceResponse AnnounceUsedSpace(AnnounceUsedSpaceRequest request, CancellationToken context)
        {
            return UsedSpaceService.AnnounceUsedSpace(request, context);
        }

        public DeleteResponse Delete(DeleteRequest request)
        {
            byte[] sig = request.Body.Signature.Sign.ToByteArray();
            MorphClient.DeleteContainer(request.Body.ContainerId, sig, GetSessionToken(request));
            var resp = new DeleteResponse
            {
                Body = new DeleteResponse.Types.Body { }
            };
            return resp;
        }

        public GetResponse Get(GetRequest request)
        {
            var cnr = MorphContractInvoker.GetContainer(MorphClient, request.Body.ContainerId);
            var resp = new GetResponse
            {
                Body = new GetResponse.Types.Body
                {
                    Container = cnr.Container,
                    Signature = cnr.Signature,
                    SessionToken = cnr.SessionToken
                }
            };
            return resp;
        }

        public GetExtendedACLResponse GetExtendedACL(GetExtendedACLRequest request)
        {
            var result = MorphContractInvoker.GetEACL(MorphClient, request.Body.ContainerId);
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
            var containers = MorphContractInvoker.ListContainers(MorphClient, request.Body.OwnerId);
            var resp = new ListResponse { Body = new ListResponse.Types.Body { } };
            resp.Body.ContainerIds.AddRange(containers);
            return resp;
        }

        public PutResponse Put(PutRequest request)
        {
            FSContainer container = request.Body.Container;
            MorphClient.PutContainer(container, request.Body.Signature, GetSessionToken(request));
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
            MorphClient.SetEACL(request.Body.Eacl, request.Body.Signature, GetSessionToken(request));
            var resp = new SetExtendedACLResponse
            {
                Body = new SetExtendedACLResponse.Types.Body { }
            };
            return resp;
        }

        private SessionToken GetSessionToken(IRequest request)
        {
            RequestMetaHeader meta;
            meta = request.MetaHeader;
            while (meta is not null)
                meta = meta.Origin;
            return meta.SessionToken;
        }
    }
}
