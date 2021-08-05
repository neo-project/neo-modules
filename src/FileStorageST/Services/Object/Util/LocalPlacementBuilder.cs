using System.Collections.Generic;
using System.Linq;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Network;
using Neo.FileStorage.Placement;
using FSAddress = Neo.FileStorage.API.Refs.Address;

namespace Neo.FileStorage.Storage.Services.Object.Util
{
    public class LocalPlacementBuilder : IPlacementBuilder
    {
        private readonly IPlacementBuilder builder;
        private readonly List<Address> localAddresses;

        public LocalPlacementBuilder(IPlacementBuilder builder, List<Address> addresses)
        {
            this.builder = builder;
            localAddresses = addresses;
        }

        public List<List<Node>> BuildPlacement(FSAddress address, PlacementPolicy policy)
        {
            var node_list = builder.BuildPlacement(address, policy);
            foreach (var ns in node_list)
            {
                foreach (var n in ns)
                {
                    if (localAddresses.Intersect(n.NetworkAddresses.Select(p => Network.Address.FromString(p))).Any())
                        return new List<List<Node>> { new List<Node> { n } };
                }
            }
            return null;
        }
    }
}
