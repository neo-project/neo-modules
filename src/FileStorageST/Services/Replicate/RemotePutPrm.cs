using System.Collections.Generic;
using Neo.FileStorage.API.Netmap;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.Services.Replicate
{
    public class RemotePutPrm
    {
        public NodeInfo Node;
        public FSObject Object;
    }
}
