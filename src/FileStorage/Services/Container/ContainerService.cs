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
            byte[] cid = request.Body.ContainerId.ToByteArray();
            byte[] sig = request.Body.Signature.Sign.ToByteArray();
            bool ok = MorphContractInvoker.InvokeDelete(MorphClient, cid, sig);//TODO: handle error
            var resp = new DeleteResponse
            {
                Body = new DeleteResponse.Types.Body { }
            };
            return resp;
        }

        public GetResponse Get(GetRequest request)
        {
            byte[] cid = request.Body.ContainerId.ToByteArray();
            byte[] raw = MorphContractInvoker.InvokeGetContainer(MorphClient, cid);
            var resp = new GetResponse
            {
                Body = new GetResponse.Types.Body
                {
                    Container = FSContainer.Parser.ParseFrom(raw),
                }
            };
            return resp;
        }

        public GetExtendedACLResponse GetExtendedACL(GetExtendedACLRequest request)
        {
            byte[] cid = request.Body.ContainerId.ToByteArray();
            MorphContractInvoker.EACLValues result = MorphContractInvoker.InvokeGetEACL(MorphClient, cid);
            var resp = new GetExtendedACLResponse
            {
                Body = new GetExtendedACLResponse.Types.Body
                {
                    Eacl = EACLTable.Parser.ParseFrom(result.eacl),
                    Signature = Signature.Parser.ParseFrom(result.sig),
                }
            };
            return resp;
        }

        public ListResponse List(ListRequest request)
        {
            byte[] owner = request.Body.OwnerId.Value.ToByteArray();
            byte[][] containers = MorphContractInvoker.InvokeGetContainerList(MorphClient, owner);
            var resp = new ListResponse { Body = new ListResponse.Types.Body { } };
            foreach (byte[] c in containers)
            {
                ContainerID cid = ContainerID.Parser.ParseFrom(c);
                resp.Body.ContainerIds.Add(cid);
            }
            return resp;
        }

        public PutResponse Put(PutRequest request)
        {
            FSContainer container = request.Body.Container;
            byte[] sig = request.Body.Signature.Sign.ToByteArray();
            byte[] public_key = request.Body.Signature.Key.ToByteArray();
            bool ok = MorphContractInvoker.InvokePut(MorphClient, container.ToByteArray(), sig, public_key);//TODO: handle error
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
            byte[] eacl = request.Body.Eacl.ToByteArray();
            byte[] sig = request.Body.Signature.Sign.ToByteArray();
            bool ok = MorphContractInvoker.InvokeSetEACL(MorphClient, eacl, sig);//TODO: handle error
            var resp = new SetExtendedACLResponse
            {
                Body = new SetExtendedACLResponse.Types.Body { }
            };
            return resp;
        }
    }
}
