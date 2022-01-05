using Neo.FileStorage.Reputation;
using Neo.FileStorage.Storage.Core.Container;
using Neo.FileStorage.Storage.Placement;
using System;
using FSAddress = Neo.FileStorage.API.Refs.Address;

namespace Neo.FileStorage.Storage.Services.Object.Util
{
    public class TraverserGenerator : ITraverserGenerator
    {
        private readonly INetmapSource netmapSource;
        private readonly IContainerSource containerSource;
        private readonly ILocalInfoSource localAddressesSource;
        private readonly int successAfter;
        private readonly bool trackCopies;

        public TraverserGenerator(INetmapSource netmapSource, IContainerSource containerSource, ILocalInfoSource localInfoSource, int successAfter = 0, bool trackCopies = true)
        {
            this.netmapSource = netmapSource;
            this.containerSource = containerSource;
            localAddressesSource = localInfoSource;
            this.successAfter = successAfter;
            this.trackCopies = trackCopies;
        }

        public Traverser GenerateTraverser(FSAddress address, ulong epoch)
        {
            var nm = netmapSource.GetNetMapByEpoch(epoch);
            if (nm is null)
            {
                throw new Exception(nameof(TraverserGenerator) + " could not get latest netmap");
            }
            var container = containerSource.GetContainer(address.ContainerId)?.Container;
            if (container is null)
            {
                throw new Exception(nameof(TraverserGenerator) + " could not get container");
            }
            var builder = new RemotePlacementBuilder(netmapSource, localAddressesSource);
            return new Traverser(builder, container.PlacementPolicy, address, successAfter, trackCopies);
        }
    }
}
