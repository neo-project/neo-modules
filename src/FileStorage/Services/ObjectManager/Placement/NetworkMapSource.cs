using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.Core.Netmap;

namespace Neo.FileStorage.Services.ObjectManager.Placement
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
