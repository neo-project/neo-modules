using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Invoker.Morph;
using Neo.FileStorage.Storage.Core;
using FSContainer = Neo.FileStorage.API.Container.Container;

namespace Neo.FileStorage.Storage.Services.Object.Put
{
    public class ContainerSource : IContainerSoruce
    {
        private readonly MorphInvoker morphInvoker;

        public ContainerSource(MorphInvoker invoker)
        {
            morphInvoker = invoker;
        }

        public FSContainer GetContainer(ContainerID cid)
        {
            return morphInvoker.GetContainer(cid)?.Container;
        }
    }
}
