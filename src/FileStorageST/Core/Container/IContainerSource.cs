using Neo.FileStorage.API.Refs;
using FSContainer = Neo.FileStorage.API.Container.Container;

namespace Neo.FileStorage.Storage.Core
{
    public interface IContainerSoruce
    {
        FSContainer GetContainer(ContainerID cid);
    }
}
