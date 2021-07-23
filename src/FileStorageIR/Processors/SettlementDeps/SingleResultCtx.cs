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
        public ulong eAudit;
        public DataAuditResult auditResult;
        public ContainerID cid;
        public TransferTable txTable;
        public Container cnrInfo;
        public Node[] cnrNodes;
        public Dictionary<string, Node> passNodes = new();
        public BigInteger sumSGSize;
        public BigInteger auditFee;
    }
}
