using System;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.Services.ObjectManager.Placement;
using FSAddress = Neo.FileStorage.API.Refs.Address;

namespace Neo.FileStorage.Services.Object.Util
{
    public class TraverserGenerator
    {
        private readonly Client morphClient;
        private readonly Network.Address localAddress;
        private readonly int successAfter;
        private readonly bool trackCopies;

        public TraverserGenerator(Client morph, Network.Address address, int successAfter = 0, bool trackCopies = true)
        {
            morphClient = morph;
            localAddress = address;
            this.successAfter = successAfter;
            this.trackCopies = trackCopies;
        }

        public Traverser GenerateTraverser(FSAddress address)
        {
            var nm = morphClient.InvokeSnapshot(0);
            if (nm is null)
            {
                throw new Exception(nameof(TraverserGenerator) + " could not get latest netmap");
            }
            var container = morphClient.GetContainer(address.ContainerId)?.Container;
            if (container is null)
            {
                throw new Exception(nameof(TraverserGenerator) + " could not get container");
            }
            var builder = new RemotePlacementBuilder(morphClient, localAddress);
            return new Traverser(builder, container.PlacementPolicy, address, successAfter, trackCopies);
        }
    }
}
