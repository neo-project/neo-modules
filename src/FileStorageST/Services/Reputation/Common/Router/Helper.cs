using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Reputation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.FileStorage.Storage.Services.Reputaion.Common.Route
{
    public static class Helper
    {
        public static bool CheckRoute(this IBuilder builder, ulong epoch, PeerToPeerTrust trust, List<NodeInfo> route)
        {
            for (int i = 1; i < route.Count; i++)
            {
                List<NodeInfo> servers;
                try
                {
                    servers = builder.NextStage(epoch, trust, route.Take(i).ToList());
                }
                catch (Exception e)
                {
                    Utility.Log(nameof(CheckRoute), LogLevel.Warning, $"could not build next stage, error={e.Message}");
                    return false;
                }
                if (servers.Count == 0) return true;
                bool found = false;
                for (int j = 0; j < servers.Count; j++)
                {
                    if (servers[j].PublicKey.Equals(route[i].PublicKey))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }
            return true;
        }
    }
}
