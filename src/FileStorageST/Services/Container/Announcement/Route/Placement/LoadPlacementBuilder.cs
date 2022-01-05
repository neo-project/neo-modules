using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Reputation;
using Neo.FileStorage.Storage.Core.Container;
using System;
using System.Collections.Generic;
using System.Linq;
using static Neo.Utility;

namespace Neo.FileStorage.Storage.Services.Container.Announcement
{
    public class LoadPlacementBuilder
    {
        public const string PivotPrefix = "load_announcement_";
        public INetmapSource NetmapSource { get; init; }
        public IContainerSource ContainerSoruce { get; init; }

        public List<List<Node>> BuildPlacement(ulong epoch, ContainerID cid)
        {
            byte[] pivot = StrictUTF8.GetBytes(PivotPrefix + epoch);
            var nm = NetmapSource.GetNetMapByEpoch(epoch);
            var container = ContainerSoruce.GetContainer(cid)?.Container;
            var nodes = nm.GetContainerNodes(container.PlacementPolicy, cid.Value.ToByteArray());
            return nm.GetPlacementVectors(nodes, pivot);
        }

        public bool IsNodeFromContainerKey(ulong epoch, ContainerID cid, byte[] key)
        {
            var nm = NetmapSource.GetNetMapByEpoch(epoch);
            var container = ContainerSoruce.GetContainer(cid)?.Container;
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
