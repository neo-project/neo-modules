using NeoFS.API.v2.Cryptography.Tz;
using V2Object = NeoFS.API.v2.Object.Object;
using NeoFS.API.v2.Refs;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Neo.FSNode.Services.Audit.Auditor
{
    public partial class Context
    {
        private const int hashRangeNumber = 4;
        private readonly int minGamePayloadSize = hashRangeNumber * new TzHash().HashSize;
        public IContainerCommunicator ContainerCommunacator;
        public AuditTask AuditTask;
        public ulong MaxPDPInterval;//MillisecondsTimeout
        private Report report;
        private readonly ConcurrentDictionary<string, ShortHeader> HeaderCache = default;
        private readonly List<GamePair> pairs = default;
        private readonly ConcurrentDictionary<ulong, PairMemberInfo> pairedNodes = default;
        private bool Expired => AuditTask.Context.IsCancellationRequested;

        public void Execute()
        {
            Initialize();
            ExecutePoR();
            ExecutePoP();
            ExecutePDP();
            Complete();
            WriteReport();
        }

        private void Initialize()
        {
            report = new Report();
            report.SetContainerID(AuditTask.CID);
        }

        private void Complete()
        {
            if (Expired) return;
            report.SetComplete();
        }

        private void WriteReport()
        {
            AuditTask.Reporter.WriteReport(report);
        }

        private ulong ObjectSize(ObjectID oid)
        {
            if (HeaderCache.TryGetValue(oid.ToBase58String(), out ShortHeader header))
                return header.ObjectSize;
            return 0;
        }

        private void UpdateHeader(V2Object header)
        {
            HeaderCache[header.ObjectId.ToBase58String()] = new ShortHeader
            {
                TzHash = header.Header.HomomorphicHash.Sum.ToByteArray(),
                ObjectSize = header.Header.PayloadLength,
            };
        }
    }
}
