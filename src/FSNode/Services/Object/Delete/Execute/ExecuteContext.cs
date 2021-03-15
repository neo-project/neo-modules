using NeoFS.API.v2.Object;
using NeoFS.API.v2.Tombstone;
using System;
using System.Threading;
using V2Object = NeoFS.API.v2.Object.Object;

namespace Neo.FSNode.Services.Object.Delete.Execute
{
    public class ExecuteContext
    {
        public DeleteService DeleteService;
        public CancellationToken Context;
        public DeletePrm Prm;
        public Tombstone Tombstone;
        public SplitInfo SplitInfo;
        public V2Object TombstoneObject;
        public Exception Exception;
    }
}
