using Neo.FileStorage.API.Container;
using Neo.FileStorage.Invoker.Morph;
using Neo.FileStorage.Storage.Services.Container.Cache;
using Neo.FileStorage.Storage.Services.Container.Announcement;
using System;
using System.Threading;
using static Neo.FileStorage.Storage.Helper;
using Neo.FileStorage.API.Session;

namespace Neo.FileStorage.Storage.Services.Container
{
    public class ContainerService
    {
        public const int CacheSize = 100;
        public static readonly TimeSpan CacheTTL = TimeSpan.FromSeconds(30);
        public const string SessionTokenError = "session token does not contain container context";

        public MorphInvoker MorphInvoker { get; init; }
        public UsedSpaceService UsedSpaceService { get; init; }
        public ContainerCache ContainerCache { get; init; }
        public EACLCache EACLCache { get; init; }
        public ContainerListCache ContainerListCache { get; init; }

        public AnnounceUsedSpaceResponse AnnounceUsedSpace(AnnounceUsedSpaceRequest request, CancellationToken cancellation)
        {
            return UsedSpaceService.AnnounceUsedSpace(request, cancellation);
        }

        public DeleteResponse Delete(DeleteRequest request)
        {
            var sig = request.Body?.Signature;
            var cid = request.Body?.ContainerId;
            ContainerIDCheck(cid);
            SignatureCheck(sig);
            var sessionToken = OriginalSessionToken(request.MetaHeader);
            if (sessionToken is not null && sessionToken.Body.ContextCase != SessionToken.Types.Body.ContextOneofCase.Container)
                throw new InvalidOperationException(SessionTokenError);
            MorphInvoker.DeleteContainer(cid, sig.Sign.ToByteArray(), sessionToken);
            ContainerCache.InvalidateContainer(cid);
            EACLCache.InvalidateEACL(cid);
            ContainerListCache.InvalidateContainerListByCid(cid);
            var resp = new DeleteResponse
            {
                Body = new DeleteResponse.Types.Body { }
            };
            return resp;
        }

        public GetResponse Get(GetRequest request)
        {
            var cid = request.Body?.ContainerId;
            ContainerIDCheck(cid);
            var cnr = ContainerCache.GetContainer(cid);
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
            var cid = request.Body?.ContainerId;
            ContainerIDCheck(cid);
            var result = EACLCache.GetEAclWithSignature(cid);
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
            var ownerId = request.Body?.OwnerId;
            OwnerIDCheck(ownerId);
            var containers = ContainerListCache.GetContainerList(ownerId);
            var resp = new ListResponse { Body = new ListResponse.Types.Body { } };
            resp.Body.ContainerIds.AddRange(containers);
            return resp;
        }

        public PutResponse Put(PutRequest request)
        {
            var container = request.Body?.Container;
            var signature = request.Body?.Signature;
            ContainerCheck(container);
            SignatureCheck(signature);
            var sessionToken = OriginalSessionToken(request.MetaHeader);
            if (sessionToken is not null && sessionToken.Body.ContextCase != SessionToken.Types.Body.ContextOneofCase.Container)
                throw new InvalidOperationException(SessionTokenError);
            MorphInvoker.PutContainer(container, signature, sessionToken);
            ContainerListCache.InvalidateContainerList(container.OwnerId);
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
            var eacl = request.Body?.Eacl;
            var sig = request.Body?.Signature;
            EACLCheck(eacl);
            SignatureCheck(sig);
            var sessionToken = OriginalSessionToken(request.MetaHeader);
            if (sessionToken is not null && sessionToken.Body.ContextCase != SessionToken.Types.Body.ContextOneofCase.Container)
                throw new InvalidOperationException(SessionTokenError);
            MorphInvoker.SetEACL(eacl, sig, sessionToken);
            EACLCache.InvalidateEACL(eacl.ContainerId);
            var resp = new SetExtendedACLResponse
            {
                Body = new SetExtendedACLResponse.Types.Body { }
            };
            return resp;
        }
    }
}
