using Grpc.Core;
using NeoFS.API.v2.Netmap;

namespace Neo.FSNode.Network.Transport.Netmap
{
    public class Service
    {
        private NetmapService.NetmapServiceBase srv;

        public Service(NetmapService.NetmapServiceBase service)
        {
            this.srv = service;
        }

        public LocalNodeInfoResponse LocalNodeInfo(ServerCallContext ctx, LocalNodeInfoRequest req)
        {
            return this.srv.LocalNodeInfo(req, ctx).Result;
        }
    }
}
