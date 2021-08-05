using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using static Neo.Utility;
using FSAnnouncement = Neo.FileStorage.API.Container.AnnounceUsedSpaceRequest.Types.Body.Types.Announcement;

namespace Neo.FileStorage.Storage.Services.Container.Announcement.Route
{
    public class LoadWriter : IWriter
    {
        public LoadRouter Router;
        public CancellationToken Token;
        public RouteContext RouteContext;
        public Dictionary<RouteKey, RouteValue> mRoute = new();
        public Dictionary<string, IWriter> mServers = new();

        public void Put(FSAnnouncement announcement)
        {
            RouteKey key = new()
            {
                Epoch = announcement.Epoch,
                Cid = announcement.ContainerId.String(),
            };
            bool exists = mRoute.TryGetValue(key, out RouteValue value);
            if (!exists)
            {
                var route = Router.RouteBuilder.NextStage(announcement, RouteContext.PassedRoute);
                if (route is null || !route.Any())
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
                string pk = "";
                if (remoteInfo is not null)
                    pk = remoteInfo.PublicKey.ToBase64();
                exists = mServers.TryGetValue(pk, out IWriter remoteWriter);
                if (!exists)
                {
                    try
                    {
                        var provider = Router.RemoteProvider.InitRemote(remoteInfo);
                        remoteWriter = provider.InitWriter(Token);
                    }
                    catch (Exception e)
                    {
                        Log(nameof(LoadWriter), LogLevel.Debug, $"could not initilize writer or provider, error={e.Message}");
                        continue;
                    }
                    mServers[pk] = remoteWriter;
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
