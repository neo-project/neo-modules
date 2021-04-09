using Neo.FileStorage.API.Netmap;
using System.Collections.Generic;
using System.Linq;
using FSAnnouncement = Neo.FileStorage.API.Container.AnnounceUsedSpaceRequest.Types.Body.Types.Announcement;

namespace Neo.FileStorage.Services.Container.Announcement.Route.Placement
{
    public class RouteBuilder
    {
        public LoadPlacementBuilder PlacementBuilder;
        public List<NodeInfo> NextStage(FSAnnouncement announcement, List<NodeInfo> passed)
        {
            if (1 < passed.Count) return null;
            var placement = PlacementBuilder.BuildPlacement(announcement.Epoch, announcement.ContainerId);
            return placement.Select(p => p.Any() ? p.First() : null).Where(p => p is not null).Select(p => p.Info).ToList();
        }

        public bool CheckRoute(FSAnnouncement announcement, List<NodeInfo> route)
        {
            for (int i = 0; i < route.Count; i++)
            {
                List<NodeInfo> servers;
                try
                {
                    servers = NextStage(announcement, route.Take(i).ToList());
                }
                catch
                {
                    return false;
                }
                if (servers.Count == 0) break;
                bool found = false;
                foreach (var server in servers)
                {
                    if (server.PublicKey.SequenceEqual(route[i].PublicKey))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    return false;
            }
            return true;
        }
    }
}
