using Google.Protobuf;
using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.Session;
using Neo.FileStorage.Core.Netmap;
using Neo.FileStorage.LocalObjectStorage.Engine;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.Services.Object.Acl.EAcl;
using System;
using static Neo.FileStorage.API.Acl.BearerToken.Types.Body.Types;
using static Neo.FileStorage.API.Session.ObjectSessionContext.Types;
using static Neo.FileStorage.API.Session.SessionToken.Types.Body;
using FSAction = Neo.FileStorage.API.Acl.Action;
using FSContainer = Neo.FileStorage.API.Container.Container;

namespace Neo.FileStorage.Services.Object.Acl
{
    public partial class AclChecker
    {
        public Client Morph { get; init; }
        public StorageEngine LocalStorage { get; init; }
        public EAclValidator EAclValidator { get; init; }
        public INetState NetmapState { get; init; }

        public RequestInfo CheckRequest(IRequest request, Operation op)
        {
            var cid = GetContainerIDFromRequest(request);
            var info = FindRequestInfo(request, cid, op);
            info.ObjectID = GetObjectIDFromRequest(request);
            UseObjectIDFromSession(info, request?.MetaHeader?.SessionToken);
            if (!BasicAclCheck(info) || (op == Operation.Put && !StickyBitCheck(info))) throw new Exception($"{nameof(CheckRequest)} basic acl check failed");
            if (!EAclCheck(info, null)) throw new Exception($"{nameof(CheckRequest)} eacl check failed");
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

        private RequestInfo FindRequestInfo(IRequest request, ContainerID cid, Operation op)
        {
            var container = GetContainer(cid);
            var classifiered = Classify(request, cid, container);
            if (classifiered.Item1 == Role.Unspecified)
                throw new InvalidOperationException(nameof(FindRequestInfo) + " unkown role");
            var verb = SourceVerbOfRequest(request, op);
            var info = new RequestInfo
            {
                BasicAcl = container.BasicAcl,
                Role = classifiered.Item1,
                IsInnerRing = classifiered.Item3,
                Op = verb,
                Owner = container.OwnerId,
                ContainerID = cid,
                SenderKey = classifiered.Item2,
                Bearer = request.MetaHeader.BearerToken,
                Request = request,
            };
            return info;
        }

        private Operation SourceVerbOfRequest(IRequest request, Operation op)
        {
            if (request.MetaHeader?.SessionToken != null)
            {
                if (request.MetaHeader.SessionToken.Body?.ContextCase == ContextOneofCase.Object)
                {
                    var ctx = request.MetaHeader.SessionToken.Body?.Object;
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

        public bool BasicAclCheck(RequestInfo info)
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
            if (!info.BasicAcl.BearsAllowed(info.Op)) return false;
            if (!IsValidBearer(info, NetmapState)) return false;
            var unit = new ValidateUnit
            {
                Cid = info.ContainerID,
                Role = info.Role,
                Op = info.Op,
                Bearer = info.Bearer,
                HeaderSource = new HeaderSource(LocalStorage, info.Address, info.Request, resp),
            };
            var action = EAclValidator.CalculateAction(unit);
            return FSAction.Allow == action;
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
            if (owner is null || info.SenderKey is null || info.SenderKey.Length == 0)
                return false;
            if (!info.BasicAcl.Sticky())
                return false;
            return info.SenderKey.PublicKeyToOwnerID().Value == owner.Value;
        }

        private bool IsValidBearer(RequestInfo info, INetState state)
        {
            if (info.Bearer is null || info.Bearer.Body is null && info.Bearer.Signature is null)
                return true;

            if (!IsValidLifetime(info.Bearer.Body.Lifetime, state.CurrentEpoch()))
                return false;
            if (!info.Bearer.Signature.VerifyMessagePart(info.Bearer.Body))
                return false;
            var tokenIssueKey = info.Bearer.Signature.Key.ToByteArray();
            if (info.Owner.Value != tokenIssueKey.PublicKeyToOwnerID().Value)
                return false;
            var tokenOwnerField = info.Bearer.Body.OwnerId;
            if (tokenOwnerField.Value != info.SenderKey.PublicKeyToOwnerID().Value)
                return false;
            return true;
        }

        private bool IsValidLifetime(TokenLifetime lifetime, ulong epoch)
        {
            return epoch >= lifetime.Nbf && epoch <= lifetime.Exp;
        }

        private FSContainer GetContainer(ContainerID cid)
        {
            return FSContainer.Parser.ParseFrom(MorphContractInvoker.InvokeGetContainer(Morph, cid.ToByteArray()));
        }
    }
}
