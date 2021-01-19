using NeoFS.API.v2.Acl;
using NeoFS.API.v2.Refs;

namespace Neo.FSNode.Core.Container
{
    public interface IEAclStorage
    {
        EACLTable GetEACL(ContainerID cid);
    }
}
