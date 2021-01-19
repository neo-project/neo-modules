using NeoFS.API.v2.Netmap;
using NeoFS.API.v2.Refs;
using Neo.FSNode.Services.ObjectManager.Placement;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Neo.FSNode.Services.Audit.Auditor
{
    public partial class Context
    {
        private readonly ConcurrentDictionary<int, List<ObjectID>> sgMembersCache = default;
        private readonly ConcurrentDictionary<string, List<List<Node>>> placementCache = default;
        private uint hit, miss, fail;
        private int containerNodesNumber;

        private void ExecutePoP()
        {
            if (Expired) return;
            BuildCoverage();
            report.SetPlacementResults(hit, miss, fail);
        }

        private void BuildCoverage()
        {
            var replicas = AuditTask.Container.PlacementPolicy.Replicas;
            foreach (var members in sgMembersCache.Values)
            {
                Random rand = default;
                foreach (var oid in members.OrderBy(p => rand.Next()))
                {
                    var table = BuildPlacement(oid);
                    if (table is null) continue;
                    for (int i = 0; i < table.Count && i < replicas.Count; i++)
                    {
                        ProcessObjectPlacement(oid, table[i], replicas[i].Count);
                        if (ContainerCovered()) return;
                    }
                }
            }
        }

        private bool ContainerCovered()
        {
            return containerNodesNumber <= pairedNodes.Count;
        }

        private void ProcessObjectPlacement(ObjectID oid, List<Node> nodes, uint replicas)
        {
            uint ok = 0;
            int unpaired_candidate1 = -1, unpaired_candidate2 = -1, paired_candidate = -1;
            for (int i = 0; i < nodes.Count && ok < replicas; i++)
            {
                var header = ContainerCommunacator.GetHeader(AuditTask, nodes[i], oid, false);
                if (header is null) continue;
                UpdateHeader(header);
                ok++;
                bool optimal = ok == replicas && i < replicas;
                if (ObjectSize(oid) < (ulong)minGamePayloadSize) continue;
                if (!pairedNodes.ContainsKey(nodes[i].Hash))
                {
                    if (unpaired_candidate1 < 0)
                        unpaired_candidate1 = i;
                    else if (unpaired_candidate2 < 0)
                        unpaired_candidate2 = i;
                }
                else if (paired_candidate < 0)
                {
                    paired_candidate = i;
                }
                if (optimal)
                    hit++;
                else if (ok == replicas)
                    miss++;
                else
                    fail++;
                if (0 <= unpaired_candidate1)
                {
                    if (0 <= unpaired_candidate2)
                    {
                        ComposePair(oid, nodes[unpaired_candidate1], nodes[unpaired_candidate2]);
                    }
                    else if (0 <= paired_candidate)
                    {
                        ComposePair(oid, nodes[unpaired_candidate1], nodes[paired_candidate]);
                    }
                }
            }
        }

        private void ComposePair(ObjectID oid, Node n1, Node n2)
        {
            pairs.Add(new GamePair
            {
                N1 = n1,
                N2 = n2,
                Id = oid,
            });
            pairedNodes[n1.Hash] = new PairMemberInfo
            {
                Node = n1,
            };
            pairedNodes[n2.Hash] = new PairMemberInfo
            {
                Node = n2,
            };
        }

        private List<List<Node>> BuildPlacement(ObjectID oid)
        {
            if (placementCache.TryGetValue(oid.ToBase58String(), out List<List<Node>> table))
            {
                return table;
            }
            var nn = NetmapBuilder.BuildObjectPlacement(AuditTask.Netmap, AuditTask.ContainerNodes, oid);
            if (nn is null) return null;
            placementCache[oid.ToBase58String()] = nn;
            return nn;
        }
    }
}
