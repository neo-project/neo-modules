using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.Services.ObjectManager.Placement;
using System;
using FSAddress = Neo.FileStorage.API.Refs.Address;
using FSContainer = Neo.FileStorage.API.Container.Container;

namespace Neo.FileStorage.Services.Object.Util
{
    public class TraverserGenerator
    {
        private readonly Client morphClient;
        public Network.Address localAddress;

        public TraverserGenerator(Client morph, Network.Address address)
        {
            morphClient = morph;
            localAddress = address;
        }

        public Traverser GenerateTraverser(FSAddress address)
        {
            var nm = GetLatestNetmap();
            if (nm is null)
            {
                throw new Exception(nameof(TraverserGenerator) + " could not get latest netmap");
            }
            var container = GetContainer(address.ContainerId);
            if (container is null)
            {
                throw new Exception(nameof(TraverserGenerator) + " could not get container");
            }
            var builder = new RemotePlacementBuilder(morphClient, localAddress);
            return new Traverser(builder, container.PlacementPolicy, address);
        }

        private NetMap GetLatestNetmap()
        {
            return MorphContractInvoker.InvokeSnapshot(morphClient, 0);
        }

        private FSContainer GetContainer(ContainerID cid)
        {
            return MorphContractInvoker.InvokeGetContainer(morphClient, cid);
        }
    }
}
