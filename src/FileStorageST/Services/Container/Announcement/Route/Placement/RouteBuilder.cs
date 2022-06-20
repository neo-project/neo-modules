using Neo.FileStorage.API.Netmap;
using System;
using System.Collections.Generic;
using System.Linq;
using FSAnnouncement = Neo.FileStorage.API.Container.AnnounceUsedSpaceRequest.Types.Body.Types.Announcement;

namespace Neo.FileStorage.Storage.Services.Container.Announcement.Route.Placement
{
    public class RouteBuilder
    {
        public LoadPlacementBuilder PlacementBuilder { get; init; }

        public List<NodeInfo> NextStage(FSAnnouncement announcement, List<NodeInfo> passed)
        {
            if (1 < passed.Count) return null;
            var placement = PlacementBuilder.BuildPlacement(announcement.Epoch, announcement.ContainerId);
            List<NodeInfo> result = new();
            foreach (var n in placement)
            {
                if (n.Count == 0) continue;
                var target = n.First();
                if (passed.Count == 1 && passed[0].PublicKey.SequenceEqual(target.PublicKey))
                {
                    result.Add(null);
                }
                else
                {
                    result.Add(target.Info);
                }
            }
            return result;
        }

        public bool CheckRoute(FSAnnouncement announcement, List<NodeInfo> route)
        {
            for (int i = 1; i < route.Count; i++)
            {
                List<NodeInfo> servers;
                servers = NextStage(announcement, route.Take(i).ToList());
                if (servers is null || servers.Count == 0) break;
                bool found = false;
                foreach (var server in servers)
                {
                    if (server is null) continue;
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
