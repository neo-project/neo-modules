using NeoFS.API.v2.Netmap;
using System;
using V2Version = NeoFS.API.v2.Refs.Version;

namespace Neo.Fs.Services.Netmap
{
    public class ExecutorSvc
    {
        private V2Version version;
        private NodeInfo localNodeInfo;

        public ExecutorSvc(V2Version v, NodeInfo ni)
        {
            if (v is null || ni is null)
                throw new ArgumentNullException("cannot create netmap execution service");

            this.version = v;
            this.localNodeInfo = ni;
        }

        public LocalNodeInfoResponse LocalNodeInfo()
        {
            var body = new LocalNodeInfoResponse.Types.Body() { Version = this.version, NodeInfo = this.localNodeInfo };
            return new LocalNodeInfoResponse() { Body = body };
        }
    }
}
