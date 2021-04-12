using Neo.FileStorage.Services.Container.Announcement.Control;
using System;
using System.Collections.Generic;
using System.Threading;
using FSAnnouncement = Neo.FileStorage.API.Container.AnnounceUsedSpaceRequest.Types.Body.Types.Announcement;
using static Neo.Utility;

namespace Neo.FileStorage.Services.Container.Announcement.Route
{
    public class LoadWriter
    {
        public Router Router;
        public CancellationToken Cancellation;
        public RouteContext RouteContext;
        public Dictionary<RouteKey, RouteValue> mRoute = new();
        public Dictionary<string, RemoteAnnounceWriter> mServers = new();

        public void Put(FSAnnouncement announcement)
        {
            RouteKey key = new()
            {
                Epoch = announcement.Epoch,
                Cid = announcement.ContainerId.ToBase58String(),
            };
            bool exists = mRoute.TryGetValue(key, out RouteValue value);
            if (!exists)
            {
                var route = Router.RouteBuilder.NextStage(announcement, RouteContext.PassedRoute);
                if (route is null || route.Count == 0)
                    route = new() { null };
                value = new()
                {
                    Route = route,
                    Values = new() { announcement },
                };
                mRoute.Add(key, value);
            }
            foreach (var remoteInfo in value.Route)
            {
                string endpoint = "";
                if (remoteInfo is not null)
                    endpoint = remoteInfo.Address;
                exists = mServers.TryGetValue(endpoint, out RemoteAnnounceWriter remoteWriter);
                if (!exists)
                {
                    try
                    {
                        var provider = Router.remoteProvider.InitRemote(remoteInfo);
                        remoteWriter = provider.InitWriter(Cancellation);
                    }
                    catch (Exception e)
                    {
                        Log(nameof(LoadWriter), LogLevel.Debug, $"could not initilize writer or provider, error={e.Message}");
                        continue;
                    }
                    mServers[endpoint] = remoteWriter;
                }
                try
                {
                    remoteWriter.Put(announcement);
                }
                catch (Exception e)
                {
                    Log(nameof(LoadWriter), LogLevel.Debug, $"could not put announcement, error={e.Message}");
                }
            }
        }

        public void Close()
        {
            foreach (var (_, writer) in mServers)
            {
                try
                {
                    writer.Close();
                }
                catch (Exception e)
                {
                    Log(nameof(LoadWriter), LogLevel.Debug, $"could not close writer, error={e.Message}");
                }
            }
        }
    }
}
