using V2Container = NeoFS.API.v2.Container.Container;
using NeoFS.API.v2.Netmap;
using NeoFS.API.v2.Refs;
using System.Collections.Generic;
using System.Threading;

namespace Neo.FSNode.Services.Audit
{
    public class AuditTask
    {
        public CancellationToken Context;
        public IReporter Reporter;
        public Auditor.Context Auditor;
        public ContainerID CID;
        public V2Container Container;
        public NetMap Netmap;
        public List<List<Node>> ContainerNodes;
        public List<ObjectID> SGList;
    }
}
