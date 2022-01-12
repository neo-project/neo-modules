using System;
using System.Collections.Generic;
using System.Linq;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Reputation;

namespace Neo.FileStorage.Reputation
{
    public class ManagerBuilder
    {
        public INetmapSource NetmapSource { get; init; }

        public List<NodeInfo> BuilderManagers(ulong epoch, PeerID peer)
        {
            var nm = NetmapSource.GetNetMapByEpoch(epoch);
            var nodes = nm.Nodes.OrderBy(p => p.ID.Distance(epoch)).ToArray();
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i].PublicKey.SequenceEqual(peer.PublicKey.ToByteArray()))
                {
                    var managerIndex = i + 1;
                    if (managerIndex == nodes.Length)
                        return new() { nodes[0].Info };
                    else
                        return new() { nodes[managerIndex].Info };
                }
            }
            return new();
        }
    }
}
