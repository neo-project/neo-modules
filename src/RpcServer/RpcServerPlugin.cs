// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Network.RPC is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Collections.Generic;
using System.Linq;

namespace Neo.Plugins
{
    public class RpcServerPlugin : Plugin
    {
        public override string Name => "RpcServer";
        public override string Description => "Enables RPC for the node";

        private Settings _settings;
        private static readonly Dictionary<uint, RpcServer> Servers = new();
        private static readonly Dictionary<uint, List<object>> Handlers = new();

        protected override void Configure()
        {
            _settings = new Settings(GetConfiguration());
            foreach (RpcServerSettings s in _settings.Servers)
                if (Servers.TryGetValue(s.Network, out RpcServer server))
                    server.UpdateSettings(s);
        }

        public override void Dispose()
        {
            foreach (var (_, server) in Servers)
                server.Dispose();
            base.Dispose();
        }

        protected override void OnSystemLoaded(NeoSystem system)
        {
            RpcServerSettings s = _settings.Servers.FirstOrDefault(p => p.Network == system.Settings.Network);
            if (s is null) return;

            RpcServer server = new(system, s);

            if (Handlers.Remove(s.Network, out var list))
            {
                foreach (var handler in list)
                {
                    server.RegisterMethods(handler);
                }
            }

            server.StartRpcServer();
            Servers.TryAdd(s.Network, server);
        }

        public static void RegisterMethods(object handler, uint network)
        {
            if (Servers.TryGetValue(network, out RpcServer server))
            {
                server.RegisterMethods(handler);
                return;
            }
            if (!Handlers.TryGetValue(network, out var list))
            {
                list = new List<object>();
                Handlers.Add(network, list);
            }
            list.Add(handler);
        }
    }
}
