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
        public ulong Epoch;
        public DataAuditResult AuditResult;
        public ContainerID ContainerID;
        public TransferTable TxTable;
        public Container Container;
        public Node[] ContainerNodes;
        public Dictionary<string, Node> PassedNodes = new();
        public BigInteger SumSGSize;
        public BigInteger AuditFee;
    }
}
