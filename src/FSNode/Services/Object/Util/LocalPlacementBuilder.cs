using NeoFS.API.v2.Netmap;
using V2Address = NeoFS.API.v2.Refs.Address;
using Neo.FSNode.Network;
using static Neo.FSNode.Network.Address;
using Neo.FSNode.Services.ObjectManager.Placement;
using System.Collections.Generic;

namespace Neo.FSNode.Services.Object.Util
{
    public class LocalPlacementBuilder : NetmapBuilder
    {
        private readonly ILocalAddressSource localAddressSource;

        public LocalPlacementBuilder(NetmapSource netmap_source, ILocalAddressSource address_source)
        : base(netmap_source)
        {
            localAddressSource = address_source;
        }

        public override List<List<Node>> BuildPlacement(V2Address address, PlacementPolicy policy)
        {
            var node_list = base.BuildPlacement(address, policy);
            foreach (var ns in node_list)
            {
                foreach (var n in ns)
                {
                    var addr = AddressFromString(n.NetworkAddress);
                    if (addr.IsLocalAddress(localAddressSource))
                        return new List<List<Node>> { new List<Node> { n } };
                }
            }
            return null;
        }
    }
}
