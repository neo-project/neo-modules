using NeoFS.API.v2.Netmap;
using Neo.FSNode.Core.Netmap;

namespace Neo.FSNode.Services.ObjectManager.Placement
{
    public class NetworkMapSource : INetmapSource
    {
        private readonly NetMap netmap;

        public NetworkMapSource(NetMap netmap)
        {
            this.netmap = netmap;
        }

        public NetMap GetNetMap(ulong diff)
        {
            return netmap;
        }
    }
}
