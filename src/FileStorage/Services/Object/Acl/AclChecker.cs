using Neo.FileStorage.API.Acl;
using V2Action = Neo.FileStorage.API.Acl.Action;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.Session;
using Neo.FileStorage.LocalObjectStorage.LocalStore;
using Neo.FileStorage.Core.Container;
using Neo.FileStorage.Core.Netmap;
using Neo.FileStorage.Services.Object.Acl.EAcl;
using System;
using static Neo.FileStorage.API.Acl.BearerToken.Types.Body.Types;
using static Neo.FileStorage.API.Session.ObjectSessionContext.Types;
using static Neo.FileStorage.API.Session.SessionToken.Types.Body;

namespace Neo.FileStorage.Services.Object.Acl
{
    public class AclChecker
    {
        private readonly IContainerSource containerSource;
        private readonly Storage localStorage;
        private readonly EAclValidator eAclValidator;
        private readonly Classifier classifier;
        private readonly INetState netmapState;

        public AclChecker(IContainerSource cs, Storage local_storage, IEAclSource source, Classifier classifier, INetState state)
        {
            containerSource = cs;
            localStorage = local_storage;
            eAclValidator = new EAclValidator(source);
            this.classifier = classifier;
            netmapState = state;
        }

        public ContainerID GetContainerIDFromRequest(IRequest req)
        {
            switch (req)
            {
                case GetRequest getReq:
                    return getReq.Body.Address.ContainerId;
                case PutRequest putReq:
                    var obj = putReq.Body.Init;
                    if (obj is null) throw new InvalidOperationException(nameof(GetContainerIDFromRequest) + " cannt get cid from chunk");
                    return obj.Header.ContainerId;
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

        public RequestInfo FindRequestInfo(IRequest request, ContainerID cid, Operation op)
        {
            var container = containerSource.Get(cid);
            classifier.Classify(request, cid, container);
            if (classifier.Role == Role.Unspecified)
                throw new InvalidOperationException(nameof(FindRequestInfo) + " unkown role");
            var verb = SourceVerbOfRequest(request, op);
            var info = new RequestInfo
            {
                BasicAcl = container.BasicAcl,
                Role = classifier.Role,
                IsInnerRing = classifier.IsInnerRing,
                Op = verb,
                Owner = container.OwnerId,
                ContainerID = cid,
                SenderKey = classifier.SenderKey,
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

        public bool EAclCheck(object message, RequestInfo info)
        {
            if (info.BasicAcl.Final()) return true;
            if (!info.BasicAcl.BearsAllowed(info.Op)) return false;
            if (!IsValidBearer(info, netmapState)) return false;
            var unit = new ValidateUnit
            {
                Cid = info.ContainerID,
                Role = info.Role,
                Op = info.Op,
                Bearer = info.Bearer,
                HeaderSource = new HeaderSource(localStorage, message),
            };
            var action = eAclValidator.CalculateAction(unit);
            return V2Action.Allow == action;
        }

        public bool StickyBitCheck(RequestInfo info, OwnerID owner)
        {
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
    }
}