using System;
using System.Collections.Generic;
using System.Linq;
using Neo.FileStorage.API.Reputation;

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
                if (!route.Any()) route = new() { null };
                foreach (var remote in route)
                {
                    string key = "";
                    if (remote is not null) key = remote.PublicKey.ToBase64();
                    if (!servers.TryGetValue(key, out IWriter writer))
                    {
                        try
                        {
                            var provider = Router.RemoteWriterProvider.InitRemote(remote);
                            writer = provider.InitWriter(RouteContext);
                            servers[key] = writer;
                        }
                        catch (Exception)
                        {
                            continue;
                        }
                    }
                    try
                    {
                        writer.Write(trust);
                    }
                    catch { }
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
