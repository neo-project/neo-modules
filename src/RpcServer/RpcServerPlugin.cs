using System.Collections.Generic;

namespace Neo.Plugins
{
    public class RpcServerPlugin : Plugin
    {
        static List<object> handlers = new List<object>();
        public override string Name => "RpcServer";
        public override string Description => "Enables RPC for the node";
        RpcServer server;
        RpcServerSettings settings;

        protected override void Configure()
        {
            var loadedSettings = new RpcServerSettings(GetConfiguration());
            if (this.settings == null)
            {
                this.settings = loadedSettings;
            }
            else
            {
                this.settings.UpdateSettings(loadedSettings);
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            if (server != null)
            {
                server.Dispose();
                server = null;
            }
        }

        protected override void OnPluginsLoaded()
        {
            this.server = new RpcServer(System, settings);

            foreach (var handler in handlers)
            {
                this.server.RegisterMethods(handler);
            }
            handlers.Clear();

            server.StartRpcServer();
        }

        public static void RegisterMethods(object handler)
        {
            handlers.Add(handler);
        }
    }
}
