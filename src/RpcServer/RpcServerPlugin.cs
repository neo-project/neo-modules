// Copyright (C) 2015-2021 The Neo Project.
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
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;

namespace Neo.Plugins
{
    public class RpcServerPlugin : Plugin
    {
        public override string Name => "RpcServer";
        public override string Description => "Enables RPC for the node";

        private Settings settings;
        private static readonly Dictionary<uint, RpcServer> servers = new();
        private static readonly Dictionary<uint, List<object>> handlers = new();

        /// <summary>
        /// Public interface of _logEvents.
        /// </summary>
        public static Dictionary<UInt256, Queue<LogEventArgs>> LogEvents { get; } = new();

        /// <summary>
        /// Maximum number of events to be logged per contract
        /// </summary>
        private const int MaxLogEvents = 50;

        protected override void Configure()
        {
            settings = new Settings(GetConfiguration());
            foreach (RpcServerSettings s in settings.Servers)
                if (servers.TryGetValue(s.Network, out RpcServer server))
                    server.UpdateSettings(s);
        }

        private void OnCommitted(NeoSystem system, Block block)
        {
            LogEvents.Clear();
        }

        protected override void OnSystemLoaded(NeoSystem system)
        {
            RpcServerSettings s = settings.Servers.FirstOrDefault(p => p.Network == system.Settings.Network);
            if (s is null) return;

            RpcServer server = new(system, s);

            if (handlers.Remove(s.Network, out var list))
            {
                foreach (var handler in list)
                {
                    server.RegisterMethods(handler);
                }
            }

            server.StartRpcServer();
            servers.TryAdd(s.Network, server);

            ApplicationEngine.Log += Ev;
            Blockchain.Committed += OnCommitted;
        }

        // It is potentially possible to have dos attack by sending a lot of transactions and logs.
        // To prevent this, we limit the number of logs to be logged per contract.
        // If the number of logs is greater than MAX_LOG_EVENTS, we remove the oldest log.
        private static void Ev(object _, LogEventArgs e)
        {
            if (e.ScriptContainer is not Transaction tx) return;
            if (!LogEvents.TryGetValue(tx.Hash, out var _logs))
            {
                _logs = new Queue<LogEventArgs>();
                LogEvents.Add(tx.Hash, _logs);
            }
            if (LogEvents[tx.Hash].Count >= MaxLogEvents)
            {
                _logs.Dequeue();
            }
            _logs.Enqueue(e);
        }

        public override void Dispose()
        {
            Blockchain.Committed -= OnCommitted;
            ApplicationEngine.Log -= Ev;
            foreach (var (_, server) in servers)
                server.Dispose();
            base.Dispose();
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
