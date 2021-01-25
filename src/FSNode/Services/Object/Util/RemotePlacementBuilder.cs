using NeoFS.API.v2.Netmap;
using Neo.FSNode.Core.Netmap;
using Neo.FSNode.Network;
using Neo.FSNode.Services.ObjectManager.Placement;
using static Neo.FSNode.Network.Address;
using System;
using System.Collections.Generic;
using V2Address = NeoFS.API.v2.Refs.Address;

namespace Neo.FSNode.Services.Object.Util
{
    public class RemotePlacementBuilder : NetworkMapBuilder
    {
        private readonly ILocalAddressSource localAddressSource;

        public RemotePlacementBuilder(INetmapSource netmap_source, ILocalAddressSource local_address_source)
        : base(netmap_source)
        {
            localAddressSource = local_address_source;
        }

        public override List<List<Node>> BuildPlacement(V2Address address, PlacementPolicy policy)
        {
            var node_list = base.BuildPlacement(address, policy);
            foreach (var ns in node_list)
            {
                foreach (var n in ns)
                {
                    Address addr;
                    try
                    {
                        addr = AddressFromString(n.NetworkAddress);
                    }
                    catch (Exception e)
                    {
                        Utility.Log(nameof(RemotePlacementBuilder), LogLevel.Error, e.Message);
                        continue;
                    }
                    if (addr.IsLocalAddress(localAddressSource))
                        ns.Remove(n);
                }
            }
            return null;
        }
    }
}
