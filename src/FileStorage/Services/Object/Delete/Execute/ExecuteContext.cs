using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Tombstone;
using System;
using System.Threading;
using V2Object = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Services.Object.Delete.Execute
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
