using V2Cotainer = NeoFS.API.v2.Container;
using NeoFS.API.v2.Refs;

namespace Neo.FSNode.Core.Container
{
    public interface IContainerSource
    {
        V2Cotainer.Container Get(ContainerID cid);
    }
}
