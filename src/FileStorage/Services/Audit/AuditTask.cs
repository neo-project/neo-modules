using V2Container = Neo.FileStorage.API.Container.Container;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using System.Collections.Generic;
using System.Threading;

namespace Neo.FileStorage.Services.Audit
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
