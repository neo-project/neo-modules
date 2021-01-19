using NeoFS.API.v2.Acl;
using NeoFS.API.v2.Refs;

namespace Neo.FSNode.Services.Object.Acl.EAcl
{
    public class ValidateUnit
    {
        public ContainerID Cid;
        public Role Role;
        public Operation Op;
        public ITypedHeaderSource HeaderSource;
        public byte[] Key;
        public BearerToken Bearer;
    }
}
