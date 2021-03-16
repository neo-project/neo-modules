using V2Cotainer = Neo.FileStorage.API.Container;
using Neo.FileStorage.API.Refs;

namespace Neo.FileStorage.Core.Container
{
    public interface IContainerSource
    {
        V2Cotainer.Container Get(ContainerID cid);
    }
}
