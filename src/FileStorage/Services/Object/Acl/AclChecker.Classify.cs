using FSContainer = Neo.FileStorage.API.Container.Container;
using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.Session;
using Neo.FileStorage.Morph.Invoker;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Neo.FileStorage.Services.Object.Acl
{
    public partial class AclChecker
    {
        private (Role, byte[], bool) Classify(IRequest request, ContainerID cid, FSContainer container)
        {
            if (cid is null)
                throw new ArgumentNullException(nameof(cid));
            Role role;
            bool is_inner = false;
            byte[] public_key = RequestOwner(request);
            OwnerID owner = public_key.PublicKeyToOwnerID();
            if (owner == container.OwnerId)
                role = Role.User;
            else if (is_inner = IsInnerRingKey(public_key))
                role = Role.System;
            else if (IsContainerKey(public_key, cid, container))
                role = Role.System;
            else
                role = Role.Others;
            return (role, public_key, is_inner);
        }

        private byte[] RequestOwner(IRequest request)
        {
            if (request.VerifyHeader is null)
                throw new ArgumentException($"{nameof(RequestOwner)} no verification header");
            if (request.MetaHeader?.SessionToken?.Body is not null)
            {
                return OwnerFromToken(request.MetaHeader.SessionToken);
            }
            return OriginalBodySignature(request.VerifyHeader).Key.ToByteArray();
        }

        private byte[] OwnerFromToken(SessionToken token)
        {
            if (!token.Signature.VerifyMessagePart(token.Body)) throw new InvalidOperationException($"{nameof(OwnerFromToken)} invalid session token signature");
            var tokenIssueKey = token.Signature.Key.ToByteArray();
            var tokenOwner = token.Body.OwnerId;
            if (tokenIssueKey.PublicKeyToOwnerID() != tokenOwner) throw new InvalidOperationException($"{nameof(OwnerFromToken)} invalid session token owner");
            return tokenIssueKey;
        }

        private Signature OriginalBodySignature(RequestVerificationHeader verification)
        {
            if (verification is null) throw new InvalidOperationException($"{nameof(RequestOwner)} no body signature");
            while (verification.Origin is not null)
                verification = verification.Origin;
            return verification.BodySignature;
        }

        private bool IsInnerRingKey(byte[] key)
        {
            foreach (var k in InnerRingKeys())
                if (k.SequenceEqual(key)) return true;
            return false;
        }

        private bool IsContainerKey(byte[] key, ContainerID cid, FSContainer container)
        {
            try
            {
                var nm = GetLatestNetworkMap();
                var is_in = LookUpKeyInContainer(nm, key, cid, container);
                if (is_in) return true;
                nm = GetPreviousNetworkMap();
                return LookUpKeyInContainer(nm, key, cid, container);
            }
            catch
            {
                return false;
            }
        }

        private bool LookUpKeyInContainer(NetMap nm, byte[] key, ContainerID cid, FSContainer container)
        {
            var nodes = nm.GetContainerNodes(container.PlacementPolicy, cid.Value.ToByteArray());
            if (nodes is null) throw new InvalidOperationException(nameof(LookUpKeyInContainer) + " cannt get container nodes");
            var ns = nodes.Flatten();
            foreach (var n in ns)
                if (n.PublicKey.SequenceEqual(key)) return true;
            return false;
        }

        private List<byte[]> InnerRingKeys()
        {
            return Morph.NeoFSAlphabetList().Select(p => p.EncodePoint(true)).ToList();
        }

        private NetMap GetLatestNetworkMap()
        {
            return MorphContractInvoker.InvokeSnapshot(Morph, 0);
        }

        private NetMap GetPreviousNetworkMap()
        {
            return MorphContractInvoker.InvokeSnapshot(Morph, 1);
        }
    }
}
