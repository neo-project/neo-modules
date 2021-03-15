using System.Collections.Generic;
using System.Linq;

namespace Neo.Plugins
{
    public class RpcServerPlugin : Plugin
    {
        public override string Name => "RpcServer";
        public override string Description => "Enables RPC for the node";

        private Settings settings;
        private static readonly Dictionary<uint, RpcServer> servers = new Dictionary<uint, RpcServer>();
        private static readonly Dictionary<uint, List<object>> handlers = new Dictionary<uint, List<object>>();

        protected override void Configure()
        {
            settings = new Settings(GetConfiguration());
            foreach (RpcServerSettings s in settings.Servers)
                if (servers.TryGetValue(s.Network, out RpcServer server))
                    server.UpdateSettings(s);
        }

        public override void Dispose()
        {
            foreach (var (_, server) in servers)
                server.Dispose();
            base.Dispose();
        }

        protected override void OnSystemLoaded(NeoSystem system)
        {
            RpcServerSettings s = settings.Servers.FirstOrDefault(p => p.Network == system.Settings.Magic);
            if (s is null) return;

            RpcServer server = new RpcServer(system, s);

            if (handlers.Remove(s.Network, out var list))
            {
                foreach (var handler in list)
                {
                    server.RegisterMethods(handler);
                }
            }

            server.StartRpcServer();
            servers.TryAdd(s.Network, server);
        }

        public static void RegisterMethods(object handler, uint network)
        {
            if (servers.TryGetValue(network, out RpcServer server))
            {
                server.RegisterMethods(handler);
                return;
            }
            if (!handlers.TryGetValue(network, out var list))
            {
                list = new List<object>();
                handlers.Add(network, list);
            }
            list.Add(handler);
        }
    }
}
