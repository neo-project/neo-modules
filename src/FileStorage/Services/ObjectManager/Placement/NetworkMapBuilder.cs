using Neo.FileStorage.Core.Netmap;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using System;
using System.Collections.Generic;

namespace Neo.FileStorage.Services.ObjectManager.Placement
{
    public class NetworkMapBuilder : IPlacementBuilder
    {
        private readonly INetmapSource netmapSource;

        public NetworkMapBuilder(INetmapSource source)
        {
            netmapSource = source;
        }

        public NetworkMapBuilder(NetMap netMap)
        {
            netmapSource = new NetworkMapSource(netMap);
        }

        public virtual List<List<Node>> BuildPlacement(Address address, PlacementPolicy policy)
        {
            var netmap = netmapSource.GetLatestNetworkMap();
            var nodes = netmap.GetContainerNodes(policy, address.ContainerId.Value.ToByteArray());
            return BuildObjectPlacement(netmap, nodes, address.ObjectId);
        }

        public static List<List<Node>> BuildObjectPlacement(NetMap netmap, List<List<Node>> container_nodes, ObjectID oid)
        {
            if (oid is null)
                return container_nodes;
            var ns = netmap.GetPlacementVectors(container_nodes, oid.Value.ToByteArray());
            if (ns is null)
                throw new InvalidOperationException(nameof(BuildObjectPlacement) + " could not get placement vectors for object");
            return ns;
        }
    }
}
