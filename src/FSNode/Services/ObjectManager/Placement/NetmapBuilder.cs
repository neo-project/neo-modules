using Neo.FSNode.Core.Netmap;
using NeoFS.API.v2.Netmap;
using NeoFS.API.v2.Refs;
using System;
using System.Collections.Generic;

namespace Neo.FSNode.Services.ObjectManager.Placement
{
    public class NetmapBuilder : IBuilder
    {
        private readonly INetmapSource netmapSource;

        public NetmapBuilder(INetmapSource source)
        {
            netmapSource = source;
        }

        public NetmapBuilder(NetMap netMap)
        {
            netmapSource = new NetmapSource(netMap);
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

    public class NetmapSource : INetmapSource
    {
        private readonly NetMap netmap;

        public NetmapSource(NetMap netmap)
        {
            this.netmap = netmap;
        }

        public NetMap GetNetMap(ulong diff)
        {
            return netmap;
        }
    }
}
