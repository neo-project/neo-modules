using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Refs;

namespace Neo.FileStorage.Core.Container
{
    public interface IEAclSource
    {
        EACLTable GetEACL(ContainerID cid);
    }
}
