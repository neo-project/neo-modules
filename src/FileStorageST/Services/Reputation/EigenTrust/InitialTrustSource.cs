using Neo.FileStorage.API.Reputation;
using Neo.FileStorage.Storage.Cache;
using System.Linq;

namespace Neo.FileStorage.Storage.Services.Reputaion.EigenTrust
{
    public class InitialTrustSource
    {
        public NetmapCache NetmapCache { get; init; }

        public double InitialTrust(PeerID peer)
        {
            var nm = NetmapCache.GetNetMapByDiff(1);
            if (nm.Nodes.Count == 0) return 0;
            return 1.0 / nm.Nodes.Count;
        }
    }
}
