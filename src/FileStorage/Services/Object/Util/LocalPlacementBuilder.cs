using Neo.FileStorage.API.Netmap;
using V2Address = Neo.FileStorage.API.Refs.Address;
using Neo.FileStorage.Network;
using static Neo.FileStorage.Network.Address;
using Neo.FileStorage.Services.ObjectManager.Placement;
using System.Collections.Generic;
using Neo.FileStorage.Core.Netmap;

namespace Neo.FileStorage.Services.Object.Util
{
    public class LocalPlacementBuilder : NetworkMapBuilder
    {
        private readonly ILocalAddressSource localAddressSource;

        public LocalPlacementBuilder(INetmapSource netmap_source, ILocalAddressSource address_source)
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
