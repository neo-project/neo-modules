using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Invoker.Morph;
using FSContainer = Neo.FileStorage.API.Container.Container;

namespace Neo.FileStorage.Storage.Core.Container
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
