using NeoFS.API.v2.Netmap;

namespace Neo.FSNode.Core.Netmap
{
    public static class Helper
    {
        public static NetMap GetLatestNetworkMap(this INetmapSource src)
        {
            return src.GetNetMap(0);
        }

        public static NetMap GetPreviousNetworkMap(this INetmapSource src)
        {
            return src.GetNetMap(1);
        }
    }
}
