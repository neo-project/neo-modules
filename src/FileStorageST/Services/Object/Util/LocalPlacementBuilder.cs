using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Network;
using Neo.FileStorage.Placement;
using System;
using System.Collections.Generic;
using System.Linq;
using FSAddress = Neo.FileStorage.API.Refs.Address;

namespace Neo.FileStorage.Storage.Services.Object.Util
{
    public class LocalPlacementBuilder : IPlacementBuilder
    {
        private readonly IPlacementBuilder builder;
        private readonly ILocalInfoSource localInfoSource;

        public LocalPlacementBuilder(IPlacementBuilder builder, ILocalInfoSource localInfo)
        {
            this.builder = builder;
            localInfoSource = localInfo;
        }

        public List<List<Node>> BuildPlacement(FSAddress address, PlacementPolicy policy)
        {
            var nss = builder.BuildPlacement(address, policy);
            foreach (var ns in nss)
            {
                foreach (var n in ns)
                {
                    if (localInfoSource.PublicKey.SequenceEqual(n.PublicKey))
                        return new List<List<Node>> { new List<Node> { n } };
                }
            }
            throw new InvalidOperationException("local node is outside of object placement");
        }
    }
}
