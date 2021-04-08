using System;
using System.Collections.Generic;
using System.Linq;
using Neo.FileStorage.API.Netmap;
using FSAnnouncement = Neo.FileStorage.API.Container.AnnounceUsedSpaceRequest.Types.Body.Types.Announcement;

namespace Neo.FileStorage.Services.Container.Announcement.Route
{
    public static class Helper
    {
        public static bool CheckRoute(IBuilder builder, FSAnnouncement announcement, List<NodeInfo> route)
        {
            for (int i = 0; i < route.Count; i++)
            {
                List<NodeInfo> servers;
                try
                {
                    servers = builder.NextStage(announcement, route.Take(i).ToList());
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
