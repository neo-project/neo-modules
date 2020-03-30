using System.Collections.Generic;

namespace Neo.Plugins
{
    public sealed class RpcServerPlugin : Plugin
    {
        static List<object> handlers = new List<object>();
        RpcServer server;
        RpcServerSettings settings;

        protected override void Configure()
        {
            settings = new RpcServerSettings(GetConfiguration());
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

            server.StartHost();
        }

        public static void RegisterMethods(object handler)
        {
            // if RpcServerPlugin is already loaded, call RegisterMethods directly
            foreach (var plugin in Plugin.Plugins)
            {
                if (plugin is RpcServerPlugin rpcServerPlugin)
                {
                    rpcServerPlugin.server.RegisterMethods(handler);
                    return;
                }
            }

            // otherwise, save the handler for use during RpcServerPlugin load
            handlers.Add(handler);
        }
    }
}
