using System;
using Neo.FileStorage.Invoker.Morph;
using Neo.FileStorage.Storage.Placement;
using System.Collections.Generic;
using FSAddress = Neo.FileStorage.API.Refs.Address;

namespace Neo.FileStorage.Storage.Services.Object.Util
{
    public class TraverserGenerator : ITraverserGenerator
    {
        private readonly MorphInvoker morphInvoker;
        private readonly ILocalInfoSource localAddressesSource;
        private readonly int successAfter;
        private readonly bool trackCopies;

        public TraverserGenerator(MorphInvoker invoker, ILocalInfoSource localInfoSource, int successAfter = 0, bool trackCopies = true)
        {
            morphInvoker = invoker;
            localAddressesSource = localInfoSource;
            this.successAfter = successAfter;
            this.trackCopies = trackCopies;
        }

        public Traverser GenerateTraverser(FSAddress address, ulong epoch)
        {
            var nm = morphInvoker.GetNetMapByEpoch(epoch);
            if (nm is null)
            {
                throw new Exception(nameof(TraverserGenerator) + " could not get latest netmap");
            }
            var container = morphInvoker.GetContainer(address.ContainerId)?.Container;
            if (container is null)
            {
                throw new Exception(nameof(TraverserGenerator) + " could not get container");
            }
            var builder = new RemotePlacementBuilder(morphInvoker, localAddressesSource);
            return new Traverser(builder, container.PlacementPolicy, address, successAfter, trackCopies);
        }
    }
}
