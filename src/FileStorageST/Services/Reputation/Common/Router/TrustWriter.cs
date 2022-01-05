using Neo.FileStorage.API.Reputation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.FileStorage.Storage.Services.Reputaion.Common.Route
{
    public class TrustWriter : IWriter
    {
        public Router Router { get; init; }
        public RouteContext RouteContext { get; init; }
        private readonly Dictionary<string, IWriter> servers = new();
        private readonly object writeLock = new();

        public void Write(PeerToPeerTrust trust)
        {
            lock (writeLock)
            {
                var route = Router.RouteBuilder.NextStage(RouteContext.Epoch, trust, RouteContext.Passed);
                if (route.Count == 0) route = new() { null };
                foreach (var remote in route)
                {
                    string key = "";
                    if (remote is not null) key = remote.PublicKey.ToBase64();
                    if (!servers.TryGetValue(key, out IWriter writer))
                    {
                        try
                        {
                            var provider = Router.RemoteWriterProvider.InitRemote(remote);
                            writer = provider.InitWriter(RouteContext.CommonContext);
                            servers[key] = writer;
                        }
                        catch (Exception e)
                        {
                            Utility.Log(nameof(TrustWriter), LogLevel.Warning, $"could not init writer, error={e.Message}");
                            continue;
                        }
                    }
                    try
                    {
                        writer.Write(trust);
                    }
                    catch (Exception e)
                    {
                        Utility.Log(nameof(TrustWriter), LogLevel.Warning, $"could not write value, error={e.Message}");
                    }
                }
            }
        }

        public void Close()
        {
            foreach (var writer in servers.Values)
                writer.Close();
        }
    }
}
