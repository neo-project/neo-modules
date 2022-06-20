using Neo.FileStorage.API.Container;
using System.Threading;

namespace Neo.FileStorage.Storage.Services.Container
{
    public class ContainerSignService : SignService
    {
        public ContainerResponseService ResponseService { get; init; }

        public AnnounceUsedSpaceResponse AnnounceUsedSpace(AnnounceUsedSpaceRequest request, CancellationToken cancellation)
        {
            return (AnnounceUsedSpaceResponse)HandleUnaryRequest(request, r =>
            {
                return ResponseService.AnnounceUsedSpace((AnnounceUsedSpaceRequest)r, cancellation);
            }, () => new AnnounceUsedSpaceResponse());
        }

        public DeleteResponse Delete(DeleteRequest request)
        {
            return (DeleteResponse)HandleUnaryRequest(request, r =>
            {
                return ResponseService.Delete((DeleteRequest)r);
            }, () => new DeleteResponse());
        }

        public GetResponse Get(GetRequest request)
        {
            return (GetResponse)HandleUnaryRequest(request, r =>
            {
                return ResponseService.Get((GetRequest)r);
            }, () => new GetResponse());
        }

        public GetExtendedACLResponse GetExtendedACL(GetExtendedACLRequest request)
        {
            return (GetExtendedACLResponse)HandleUnaryRequest(request, r =>
            {
                return ResponseService.GetExtendedACL((GetExtendedACLRequest)r);
            }, () => new GetExtendedACLResponse());
        }

        public ListResponse List(ListRequest request)
        {
            return (ListResponse)HandleUnaryRequest(request, r =>
            {
                return ResponseService.List((ListRequest)r);
            }, () => new ListResponse());
        }

        public PutResponse Put(PutRequest request)
        {
            return (PutResponse)HandleUnaryRequest(request, r =>
            {
                return ResponseService.Put((PutRequest)r);
            }, () => new PutResponse());
        }

        public SetExtendedACLResponse SetExtendedACL(SetExtendedACLRequest request)
        {
            return (SetExtendedACLResponse)HandleUnaryRequest(request, r =>
            {
                return ResponseService.SetExtendedACL((SetExtendedACLRequest)r);
            }, () => new SetExtendedACLResponse());
        }
    }
}
