using Neo.FSNode.Core.Container;
using Neo.FSNode.Core.Netmap;
using Neo.FSNode.Network;
using Neo.FSNode.Services.ObjectManager.Placement;
using System;
using V2Address = NeoFS.API.v2.Refs.Address;

namespace Neo.FSNode.Services.Object.Util
{
    public class TraverserGenerator
    {
        public INetmapSource NetmapSource;
        public IContainerSource ContainerSource;
        public ILocalAddressSource LocalAddressSource;

        public TraverserGenerator(INetmapSource netmap_source, IContainerSource container_source, ILocalAddressSource address_source)
        {
            NetmapSource = netmap_source;
            ContainerSource = container_source;
            LocalAddressSource = address_source;
        }

        public Traverser GenerateTraverser(V2Address address)
        {
            var nm = NetmapSource.GetLatestNetworkMap();
            if (nm is null)
            {
                throw new Exception(nameof(TraverserGenerator) + " could not get latest netmap");
            }
            var container = ContainerSource.Get(address.ContainerId);
            if (container is null)
            {
                throw new Exception(nameof(TraverserGenerator) + " could not get container");
            }
            var builder = new RemotePlacementBuilder(NetmapSource, LocalAddressSource);
            return new Traverser()
                    .WithBuilder(builder)
                    .WithContainer(container)
                    .WithObjectID(address.ObjectId);
        }
    }
}
