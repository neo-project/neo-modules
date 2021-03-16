using Neo.FileStorage.API.Acl;
using V2Container = Neo.FileStorage.API.Container.Container;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.Session;
using Neo.FileStorage.Core.Netmap;
using System;
using System.Linq;

namespace Neo.FileStorage.Services.Object.Acl
{
    public class Classifier
    {
        private readonly IInnerRingFetcher innerRing;
        private readonly INetmapSource netmapSource;

        public Role Role;
        public bool IsInnerRing;
        public byte[] SenderKey;

        private IRequest request;
        private ContainerID containerId;
        private V2Container container;
        private OwnerID owner;

        public Classifier(IInnerRingFetcher inner_rings, INetmapSource netmap_source)
        {
            innerRing = inner_rings;
            netmapSource = netmap_source;
        }

        public void Classify(IRequest request, ContainerID cid, V2Container container)
        {

            if (containerId is null)
                throw new ArgumentNullException(nameof(cid));
            this.request = request;
            this.containerId = cid;
            this.container = container;
            RequestOwner();
            if (owner == container.OwnerId) Role = Role.User;
            if (IsInnerRingKey(SenderKey)) Role = Role.System;
            var is_cnr_node = IsContainerKey(SenderKey);
            if (is_cnr_node) Role = Role.System;
            Role = Role.Others;
        }

        private void RequestOwner()
        {
            if (request.VerifyHeader is null)
                throw new ArgumentNullException(nameof(RequestOwner) + " no verification header");
            if (request.MetaHeader?.SessionToken?.Body != null)
            {
                OwnerFromToken();
                return;
            }
            var body_sig = OriginalBodySignature();
            if (body_sig is null) throw new InvalidOperationException(nameof(RequestOwner) + " no body signature");
            SenderKey = body_sig.Key.ToByteArray();
            owner = body_sig.Key.ToByteArray().PublicKeyToOwnerID();
        }

        private void OwnerFromToken()
        {
            SessionToken token = request.MetaHeader.SessionToken;
            if (!token.Signature.VerifyMessagePart(token.Body)) throw new InvalidOperationException(nameof(OwnerFromToken) + " verify failed");
            var tokenIssueKey = token.Signature.Key.ToByteArray();
            var tokenOwner = token.Body.OwnerId;
            if (tokenIssueKey.PublicKeyToOwnerID() != tokenOwner) throw new InvalidOperationException(nameof(OwnerFromToken) + " OwnerID and key not equal");
            SenderKey = tokenIssueKey;
            owner = tokenOwner;
        }

        private Signature OriginalBodySignature()
        {
            var verification = request.VerifyHeader;
            if (verification is null) return null;
            if (verification.Origin != null) verification = verification.Origin;
            return verification.BodySignature;
        }

        private bool IsInnerRingKey(byte[] key)
        {
            var inner_ring_keys = innerRing.InnerRingKeys();
            foreach (var k in inner_ring_keys)
                if (k.SequenceEqual(key)) return true;
            return false;
        }

        private bool IsContainerKey(byte[] key)
        {
            try
            {
                var nm = netmapSource.GetLatestNetworkMap();
                var is_in = LookUpKeyInContainer(nm, key, containerId, container);
                if (is_in) return true;
                nm = netmapSource.GetPreviousNetworkMap();
                return LookUpKeyInContainer(nm, key, containerId, container);
            }
            catch
            {
                return false;
            }
        }

        private bool LookUpKeyInContainer(NetMap nm, byte[] key, ContainerID cid, V2Container container)
        {
            var nodes = nm.GetContainerNodes(container.PlacementPolicy, cid.Value.ToByteArray());
            if (nodes is null) throw new InvalidOperationException(nameof(LookUpKeyInContainer) + " cannt get container nodes");
            var ns = nodes.Flatten();
            foreach (var n in ns)
                if (n.PublicKey.SequenceEqual(key)) return true;
            return false;
        }
    }
}
