using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.Session;
using Neo.FileStorage.Invoker.Morph;
using Neo.FileStorage.Reputation;
using Neo.FileStorage.Storage.Core.Container;
using Neo.FileStorage.Storage.LocalObjectStorage.Engine;
using Neo.FileStorage.Storage.Services.Object.Acl.EAcl;
using System;
using static Neo.FileStorage.API.Acl.BearerToken.Types.Body.Types;
using static Neo.FileStorage.API.Session.ObjectSessionContext.Types;
using static Neo.FileStorage.API.Session.SessionToken.Types.Body;
using static Neo.FileStorage.Storage.Helper;
using FSAction = Neo.FileStorage.API.Acl.Action;

namespace Neo.FileStorage.Storage.Services.Object.Acl
{
    public partial class AclChecker
    {
        public MorphInvoker MorphInvoker { get; init; }
        public IContainerSource ContainerSource { get; init; }
        public INetmapSource NetmapSource { get; init; }
        public StorageEngine LocalStorage { get; init; }
        public EAclValidator EAclValidator { get; init; }
        public IEpochSource EpochSource { get; init; }

        public RequestInfo CheckRequest(IRequest request, Operation op)
        {
            var cid = GetContainerIDFromRequest(request);
            var info = FindRequestInfo(request, cid, op);
            info.ObjectID = GetObjectIDFromRequest(request);
            UseObjectIDFromSession(info, request?.MetaHeader?.SessionToken);
            if (!BasicAclCheck(info) || (op == Operation.Put && !StickyBitCheck(info)))
                throw new InvalidOperationException($"basic acl check failed");
            if (!EAclCheck(info, null)) throw new InvalidOperationException($"eacl check failed");
            return info;
        }

        private ObjectID GetObjectIDFromRequest(IRequest request)
        {
            return request switch
            {
                GetRequest getRequest => getRequest.Body.Address.ObjectId,
                DeleteRequest deleteRequest => deleteRequest.Body.Address.ObjectId,
                HeadRequest headRequest => headRequest.Body.Address.ObjectId,
                GetRangeRequest getRange => getRange.Body.Address.ObjectId,
                GetRangeHashRequest getRangeHashRequest => getRangeHashRequest.Body.Address.ObjectId,
                _ => null,
            };
        }

        private OwnerID GetOwnerIDFromRequest(object message)
        {
            switch (message)
            {
                case PutRequest putRequest:
                    if (putRequest.Body.ObjectPartCase == PutRequest.Types.Body.ObjectPartOneofCase.Init)
                        return putRequest.Body.Init.Header.OwnerId;
                    throw new InvalidOperationException(nameof(GetOwnerIDFromRequest) + " cannt get owner from chunk");
                case GetResponse getResponse:
                    if (getResponse.Body.ObjectPartCase == GetResponse.Types.Body.ObjectPartOneofCase.Init)
                        return getResponse.Body.Init.Header.OwnerId;
                    throw new InvalidOperationException(nameof(GetOwnerIDFromRequest) + " cannt get owner from chunk");
                default:
                    throw new InvalidOperationException(nameof(GetOwnerIDFromRequest) + " unsupported request type");
            }
        }

        private void UseObjectIDFromSession(RequestInfo info, SessionToken session)
        {
            var oid = session?.Body?.Object?.Address?.ObjectId;
            if (oid is null) return;
            info.ObjectID = oid;
        }

        private ContainerID GetContainerIDFromRequest(IRequest req)
        {
            switch (req)
            {
                case GetRequest getReq:
                    return getReq.Body.Address.ContainerId;
                case PutRequest putReq:
                    if (putReq.Body.ObjectPartCase == PutRequest.Types.Body.ObjectPartOneofCase.Init)
                        return putReq.Body.Init.Header.ContainerId;
                    throw new InvalidOperationException(nameof(GetContainerIDFromRequest) + " cannt get cid from chunk");
                case HeadRequest headReq:
                    return headReq.Body.Address.ContainerId;
                case SearchRequest searchReq:
                    return searchReq.Body.ContainerId;
                case DeleteRequest deleteReq:
                    return deleteReq.Body.Address.ContainerId;
                case GetRangeRequest rangeReq:
                    return rangeReq.Body.Address.ContainerId;
                case GetRangeHashRequest rangeHashReq:
                    return rangeHashReq.Body.Address.ContainerId;
                default:
                    throw new FormatException(nameof(GetContainerIDFromRequest) + " unknown request type");
            }
        }

        private SessionToken GetSessionTokenFromRequest(IRequest request, Operation op)
        {
            if (op == Operation.Put)
            {
                var putRequest = (PutRequest)request;
                if (putRequest.Body.ObjectPartCase != PutRequest.Types.Body.ObjectPartOneofCase.Init)
                    throw new InvalidOperationException($"{nameof(GetSessionTokenFromRequest)} unsupport request type");
                return putRequest.Body.Init.Header.SessionToken;
            }
            return OriginalSessionToken(request.MetaHeader);
        }

        private RequestInfo FindRequestInfo(IRequest request, ContainerID cid, Operation op)
        {
            var container = ContainerSource.GetContainer(cid).Container;
            var info = new RequestInfo
            {
                OriginalSessionToken = op == Operation.Put ? request.MetaHeader.SessionToken : GetSessionTokenFromRequest(request, op),
                Request = request,
                BasicAcl = container.BasicAcl,
                Owner = container.OwnerId,
                Bearer = OriginalBearerToken(request.MetaHeader),
                ContainerID = cid,
            };
            var classifiered = Classify(info, cid, container);
            if (classifiered.Item1 == Role.Unspecified)
                throw new InvalidOperationException(nameof(FindRequestInfo) + " unknown role");
            var verb = SourceVerbOfRequest(info, op);
            info.Role = classifiered.Item1;
            info.IsInnerRing = classifiered.Item3;
            info.Op = verb;
            info.SenderKey = classifiered.Item2;
            return info;
        }

        private Operation SourceVerbOfRequest(RequestInfo info, Operation op)
        {
            if (info.OriginalSessionToken != null)
            {
                if (info.OriginalSessionToken.Body?.ContextCase == ContextOneofCase.Object)
                {
                    var ctx = info.OriginalSessionToken.Body?.Object;
                    if (ctx is null) return op;
                    return ctx.Verb switch
                    {
                        Verb.Get => Operation.Get,
                        Verb.Put => Operation.Put,
                        Verb.Head => Operation.Head,
                        Verb.Search => Operation.Search,
                        Verb.Delete => Operation.Delete,
                        Verb.Range => Operation.Getrangehash,
                        Verb.Rangehash => Operation.Getrangehash,
                        _ => Operation.Unspecified
                    };
                }
            }
            return op;
        }

        private bool BasicAclCheck(RequestInfo info)
        {
            return info.Role switch
            {
                Role.User => info.BasicAcl.UserAllowed(info.Op),
                Role.System => info.BasicAcl.SystemAllowed(info.Op),
                Role.Others => info.BasicAcl.OthersAllowed(info.Op),
                _ => false,
            };
        }

        public bool EAclCheck(RequestInfo info, IResponse resp)
        {
            if (info.BasicAcl.Final()) return true;
            if (!info.BasicAcl.BearsAllowed(info.Op))
                info.Bearer = null;
            if (!IsValidBearer(info)) return false;
            var unit = new ValidateUnit
            {
                ContainerId = info.ContainerID,
                Role = info.Role,
                Op = info.Op,
                Bearer = info.Bearer,
                HeaderSource = new HeaderSource(LocalStorage, info.Address, info.Request, resp),
            };
            return FSAction.Allow == EAclValidator.CalculateAction(unit);
        }

        public bool StickyBitCheck(RequestInfo info)
        {
            OwnerID owner;
            try
            {
                owner = GetOwnerIDFromRequest(info.Request);
            }
            catch (Exception)
            {
                return false;
            }
            if (info.Role == Role.System)
                return true;
            if (!info.BasicAcl.Sticky())
                return true;
            if (owner is null || info.SenderKey is null || info.SenderKey.Length == 0)
                return false;
            return OwnerID.FromScriptHash(info.SenderKey.PublicKeyToScriptHash()).Equals(owner);
        }

        private bool IsValidBearer(RequestInfo info)
        {
            if (info.Bearer is null || info.Bearer.Body is null && info.Bearer.Signature is null)
                return true;

            if (!IsValidLifetime(info.Bearer.Body.Lifetime, EpochSource.CurrentEpoch))
                return false;
            if (!info.Bearer.Signature.VerifyMessagePart(info.Bearer.Body))
                return false;
            var tokenIssueKey = info.Bearer.Signature.Key.ToByteArray();
            if (!info.Owner.Equals(OwnerID.FromScriptHash(tokenIssueKey.PublicKeyToScriptHash())))
                return false;
            var tokenOwnerField = info.Bearer.Body?.OwnerId;
            if (tokenOwnerField is not null && !tokenOwnerField.Equals(OwnerID.FromScriptHash(info.SenderKey.PublicKeyToScriptHash())))
                return false;
            return true;
        }

        private bool IsValidLifetime(TokenLifetime lifetime, ulong epoch)
        {
            return epoch >= lifetime.Nbf && epoch <= lifetime.Exp;
        }
    }
}
