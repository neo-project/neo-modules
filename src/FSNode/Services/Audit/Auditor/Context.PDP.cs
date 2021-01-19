using NeoFS.API.v2.Cryptography.Tz;
using NeoFS.API.v2.Netmap;
using V2Range = NeoFS.API.v2.Object.Range;
using NeoFS.API.v2.Refs;
using static Neo.FSNode.Services.Audit.Auditor.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.FSNode.Services.Audit.Auditor
{
    public partial class Context
    {
        private void ExecutePDP()
        {
            if (Expired) return;
            ProcessPairs();
            WritePairsResult();
        }

        private void ProcessPairs()
        {
            var tasks = new Task[pairs.Count];
            for (int i = 0; i < pairs.Count; i++)
            {
                tasks[i] = Task.Run(() =>
                {
                    ProcessPair(pairs[i]);
                });
            }
            Task.WaitAll(tasks);
        }

        private void ProcessPair(GamePair pair)
        {
            DistributeRanges(pair);
            CollectHashes(pair);
            AnalyzeHashes(pair);
        }

        private void WritePairsResult()
        {
            List<byte[]> passed = default, failed = default;
            foreach (var info in pairedNodes)
            {
                if (info.Value.State)
                    passed.Add(info.Value.Node.PublicKey);
                else
                    failed.Add(info.Value.Node.PublicKey);
            }
            report.SetPDPResults(passed, failed);
        }

        private void DistributeRanges(GamePair pair)
        {
            pair.Range1 = new List<V2Range>(hashRangeNumber - 1);
            pair.Range2 = new List<V2Range>(hashRangeNumber - 1);
            for (int i = 0; i < hashRangeNumber - 1; i++)
            {
                pair.Range1[i] = new V2Range();
                pair.Range2[i] = new V2Range();
            }
            var notches = SplitPayload(pair.Id);

            pair.Range1[0].Length = notches[1];
            pair.Range1[1].Offset = notches[1];
            pair.Range1[1].Length = notches[2] - notches[1];
            pair.Range1[2].Offset = notches[2];
            pair.Range1[2].Length = notches[3] - notches[2];

            pair.Range2[0].Length = notches[0];
            pair.Range2[1].Offset = notches[0];
            pair.Range2[1].Length = notches[2] - notches[0];
            pair.Range2[2].Offset = notches[1];
            pair.Range2[2].Length = notches[3] - notches[1];
        }

        private void CollectHashes(GamePair pair)
        {
            pair.Hashes1 = new List<byte[]>(pair.Range1.Count);
            foreach (var range in pair.Range1)
            {
                Thread.Sleep((int)RandomUInt64(MaxPDPInterval));
                var hash = GetRangeHash(pair.Id, pair.N1, range);
                if (hash != null) pair.Hashes1.Add(hash);
            }
            pair.Hashes2 = new List<byte[]>(pair.Range2.Count);
            foreach (var range in pair.Range2)
            {
                Thread.Sleep((int)RandomUInt64(MaxPDPInterval));
                var hash = GetRangeHash(pair.Id, pair.N2, range);
                if (hash != null) pair.Hashes2.Add(hash);
            }
        }

        private void AnalyzeHashes(GamePair pair)
        {
            if (pair.Hashes1.Count != hashRangeNumber - 1 || pair.Hashes2.Count != hashRangeNumber - 1)
            {
                FailNodes(pair.N1, pair.N2);
                return;
            }
            var h1 = TzHash.Concat(new List<byte[]>() { pair.Hashes2[0], pair.Hashes2[1] });
            if (h1 is null || !pair.Hashes1[0].SequenceEqual(h1))
            {
                FailNodes(pair.N1, pair.N2);
                return;
            }
            var h2 = TzHash.Concat(new List<byte[]>() { pair.Hashes1[1], pair.Hashes1[2] });
            if (h2 is null || !pair.Hashes2[2].SequenceEqual(h2))
            {
                FailNodes(pair.N1, pair.N2);
                return;
            }
            var fh = TzHash.Concat(new List<byte[]>() { h1, h2 });
            var expected = ObjectHomoHash(pair.Id);
            if (fh is null || expected is null || expected.SequenceEqual(fh))
            {
                FailNodes(pair.N1, pair.N2);
                return;
            }
            PassNodesPDP(pair.N1, pair.N2);
        }

        private List<ulong> SplitPayload(ObjectID oid)
        {
            ulong prev = 0, size = ObjectSize(oid);
            var notches = new List<ulong>();
            for (int i = 0; i < hashRangeNumber; i++)
            {
                ulong next_len;
                if (i < hashRangeNumber - 1)
                    next_len = RandomUInt64(size - prev - (hashRangeNumber - 1)) + 1;
                else
                    next_len = size - prev;
                notches.Add(prev + next_len);
                prev += next_len;
            }
            return notches;
        }

        private byte[] GetRangeHash(ObjectID oid, Node node, V2Range range)
        {
            //Sleep? why
            try
            {
                var hash = ContainerCommunacator.GetRangeHash(AuditTask, node, oid, range);
                return hash;
            }
            catch
            {
                return null;
            }
        }

        private void FailNodes(params Node[] nodes)
        {
            foreach (var node in nodes)
                pairedNodes[node.Hash] = new PairMemberInfo
                {
                    State = false,
                    Node = node,
                };
        }

        private byte[] ObjectHomoHash(ObjectID oid)
        {
            if (HeaderCache.TryGetValue(oid.ToBase58String(), out ShortHeader header))
            {
                return header.TzHash;
            }
            return null;
        }

        private void PassNodesPDP(params Node[] nodes)
        {
            foreach (var node in nodes)
                pairedNodes[node.Hash] = new PairMemberInfo
                {
                    State = true,
                    Node = node,
                };
        }
    }
}
