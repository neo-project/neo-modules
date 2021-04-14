using Neo.FileStorage.Core.Container;
using Neo.FileStorage.Core.Netmap;
using Neo.FileStorage.Network;
using Neo.FileStorage.Services.ObjectManager.Placement;
using System;
using V2Address = Neo.FileStorage.API.Refs.Address;

namespace Neo.FileStorage.Services.Object.Util
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
