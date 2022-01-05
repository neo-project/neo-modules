using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Refs;

namespace Neo.FileStorage.Storage.Core.Container
{
    public interface IContainerSource
    {
        ContainerWithSignature GetContainer(ContainerID cid);
    }
}
