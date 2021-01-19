using NeoFS.API.v2.Acl;
using NeoFS.API.v2.Cryptography;
using NeoFS.API.v2.Refs;
using NeoFS.API.v2.Session;
using AclOperation = NeoFS.API.v2.Acl.Operation;

namespace Neo.FSNode.Services.Object.Acl
{
    public class RequestInfo
    {
        public uint BasicAcl;
        public Role Role;
        public bool IsInnerRing;
        public AclOperation Op;
        public OwnerID Owner;
        public ContainerID ContainerID;
        public ObjectID ObjectID;
        public byte[] SenderKey;
        public BearerToken Bearer;
        public IRequest Request;
    }
}
