using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Refs;

namespace Neo.FileStorage.Storage.Services.Object.Acl.EAcl
{
    public interface IEACLSource
    {
        EACLTable GetEACL(ContainerID cid);
    }
}
