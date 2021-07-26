using System.Collections.Generic;
using System.Numerics;
using Neo.FileStorage.API.Audit;
using Neo.FileStorage.API.Container;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;

namespace Neo.FileStorage.InnerRing.Processors
{
    public class SingleResultCtx
    {
        public DataAuditResult AuditResult { get; init; }
        public TransferTable TxTable { get; init; }
        public BigInteger AuditFee { get; init; }
        public Container Container;
        public Node[] ContainerNodes;
        public Dictionary<string, Node> PassedNodes = new();
        public BigInteger SumSGSize;

        public ulong Epoch => AuditResult.AuditEpoch;
        public ContainerID ContainerId => AuditResult.ContainerId;
    }
}
