using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Refs;

namespace Neo.FileStorage.Core.Container
{
    public interface IEAclStorage
    {
        EACLTable GetEACL(ContainerID cid);
    }
}
