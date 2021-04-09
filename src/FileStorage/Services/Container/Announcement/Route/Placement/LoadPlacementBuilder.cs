using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Core.Container;
using Neo.FileStorage.Core.Netmap;
using System;
using System.Collections.Generic;
using System.Linq;
using static Neo.Helper;
using static Neo.Utility;

namespace Neo.FileStorage.Services.Container.Announcement
{
    public class LoadPlacementBuilder
    {
        public const string PivotPrefix = "load_announcement_";
        private readonly INetmapSource netmapSource;
        private readonly IContainerSource containerSource;

        public List<List<Node>> BuildPlacement(ulong epoch, ContainerID cid)
        {
            byte[] pivot = Concat(StrictUTF8.GetBytes(PivotPrefix + epoch));
            var nm = netmapSource.GetNetMapByEpoch(epoch);
            var container = containerSource.Get(cid);
            var nodes = nm.GetContainerNodes(container.PlacementPolicy, cid.Value.ToByteArray());
            return nm.GetPlacementVectors(nodes, pivot);
        }

        public bool IsNodeFromContainerKey(ulong epoch, ContainerID cid, byte[] key)
        {
            var nm = netmapSource.GetNetMapByEpoch(epoch);
            var container = containerSource.Get(cid);
            var nodes = nm.GetContainerNodes(container.PlacementPolicy, cid.Value.ToByteArray());
            foreach (var vector in nodes)
            {
                foreach (var node in vector)
                {
                    if (node.PublicKey.SequenceEqual(key)) return true;
                }
            }
            return false;
        }
    }
}
