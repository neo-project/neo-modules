using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Refs;

namespace Neo.FileStorage.Services.Object.Acl.EAcl
{
    public class ValidateUnit
    {
        public ContainerID Cid;
        public Role Role;
        public Operation Op;
        public HeaderSource HeaderSource;
        public byte[] Key;
        public BearerToken Bearer;
    }
}
