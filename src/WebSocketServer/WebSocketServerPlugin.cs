// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Network.RPC is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Neo.ConsoleService;
using Neo.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;
using Neo.Plugins.WebSocketServer.Events;
using Neo.Plugins.WebSocketServer.Subscriptions;
using Neo.VM;

namespace Neo.Plugins.WebSocketServer
{
    public class WebSocketServerPlugin : Plugin
    {
        public override string Name => "NeoWebSocketServer";
        public override string Description => "Enables WebSocket notifications for the node";

        private const string IndexKey = "index";
        private const string TxIdKey = "txid";
        private const string ExecutionsKey = "executions";

        private static Settings _settings;
        private static WebSocketSharp.Server.WebSocketServer _server;
        private NeoSystem _system;

        private static readonly ConcurrentDictionary<string, Subscriber.WebSocketSubscriber> _subscribers = new();

        private static readonly SubscriptionManager<BlockSubscription> RefBlockSubscriptions = new();
        private static readonly SubscriptionManager<TxSubscription> RefTxSubscriptions = new();
        private static readonly SubscriptionManager<NotificationSubscription> RefNotificationSubscriptions = new();
        private static readonly SubscriptionManager<ExecutionSubscription> RefExecutionSubscriptions = new();

        public static event Handler.WebSocketEventHandler BlockEvent;
        public static event Handler.WebSocketEventHandler TransactionEvent;
        public static event Handler.WebSocketEventHandler NotificationEvent;
        public static event Handler.WebSocketEventHandler ExecutionEvent;

        public WebSocketServerPlugin()
        {
            Blockchain.Committing += OnCommitting;
        }

        protected override void Configure()
        {
            _settings ??= new Settings(GetConfiguration());
        }

        protected override void OnSystemLoaded(NeoSystem system)
        {
            _system = system;
        }

        public override void Dispose()
        {
            Blockchain.Committing -= OnCommitting;
            _server?.Stop();
            base.Dispose();
        }

        [ConsoleCommand("start wss", Category = "wss", Description = "Open Web Socket Server")]
        private void StartWebSocketServer()
        {
            if (_server is { IsListening: true }) return;

            var serverConfig = _settings.Servers.FirstOrDefault(p => p.Network == _system.Settings.Network);
            if (serverConfig == null) return;

            InitializeWebSocketServer(serverConfig);
            _server.AddWebSocketService("/", () => new Subscriber.WebSocketSubscriber());
            _server.Start();
        }

        private void InitializeWebSocketServer(WebSocketServerSetting serverConfig)
        {

            var useSsl = !string.IsNullOrEmpty(serverConfig.SslCert) && !string.IsNullOrEmpty(serverConfig.SslCertPassword);
            _server = new WebSocketSharp.Server.WebSocketServer(serverConfig.BindAddress, serverConfig.Port, useSsl);
            if (useSsl)
            {
                _server.SslConfiguration.ServerCertificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(serverConfig.SslCert, serverConfig.SslCertPassword);
            }
        }

        [ConsoleCommand("close wss", Category = "wss", Description = "Close Web Socket Server")]
        private void StopWebSocketServer()
        {
            if (_server is { IsListening: true }) _server.Stop();
            ConsoleHelper.Info("Web Socket Server closed");
        }

        private void OnCommitting(NeoSystem system, Block block, DataCache snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            if (system.Settings.Network != ProtocolSettings.Default.Network) return;

            if (!RefBlockSubscriptions.IsEmpty)
                NotifyBlockEvent(BlockLogToJson(block, applicationExecutedList));

            foreach (var appExec in applicationExecutedList.Where(p => p.Transaction != null))
            {
                if (!RefTxSubscriptions.IsEmpty)
                    NotifyTransactionEvent(appExec.Transaction.ToJson(ProtocolSettings.Default));
                if (!RefNotificationSubscriptions.IsEmpty)
                    NotifyNotificationEvent(TxLogToJson(appExec));
                if (!RefExecutionSubscriptions.IsEmpty)
                    NotifyExecutionEvent(TxLogToJson(appExec));
            }
        }

        public static bool AddSubscriber(string subscriberId, Subscriber.WebSocketSubscriber subscriber)
        {
            if (_subscribers.Count <= _settings.Servers.FirstOrDefault(p => p != null)!.MaxConcurrentConnections) return _subscribers.TryAdd(subscriberId, subscriber);
            ConsoleHelper.Error("Max concurrent connections reached");
            subscriber.Close();
            return false;
        }

        public static void RemoveSubscriber(string subscriberId)
        {
            _subscribers.TryRemove(subscriberId, out _);
        }

        private static void NotifyBlockEvent(JObject jObject)
        {
            var blockEvent = new BlockEvent
            {
                WssEvent = WssEventId.BlockEventId,
                Data = jObject,
                Height = jObject[IndexKey]!.GetInt32()
            };
            BlockEvent?.Invoke(blockEvent);
        }

        private static void NotifyTransactionEvent(JObject jObject)
        {
            var transactionEvent = new TransactionEvent
            {
                WssEvent = WssEventId.TransactionEventId,
                Data = jObject,
                Container = UInt256.Parse(jObject[TxIdKey]?.AsString())
            };
            TransactionEvent?.Invoke(transactionEvent);
        }

        private static void NotifyNotificationEvent(JObject jObject)
        {
            var notificationEvent = new NotificationEvent
            {
                WssEvent = WssEventId.NotificationEventId,
                Contract = UInt160.Parse(jObject[ExecutionsKey]?["contract"]?.AsString()),
                Data = (JObject)jObject[ExecutionsKey]?["notifications"],
            };
            NotificationEvent?.Invoke(notificationEvent);
        }

        private static void NotifyExecutionEvent(JObject jObject)
        {
            var executionEvent = new ExecutionEvent
            {
                WssEvent = WssEventId.ExecutionEventId,
                Container = UInt256.Parse(jObject[TxIdKey]?.AsString()),
                Data = jObject,
                VmState = jObject[ExecutionsKey]?["vmstate"]?.AsString(),
            };
            ExecutionEvent?.Invoke(executionEvent);
        }

        public static void AddBlockSubscription(BlockSubscription subscription)
        {
            RefBlockSubscriptions.Add(subscription);
        }

        public static void AddTransactionSubscription(TxSubscription subscription)
        {
            RefTxSubscriptions.Add(subscription);
        }

        public static void AddNotificationSubscription(NotificationSubscription subscription)
        {
            RefNotificationSubscriptions.Add(subscription);
        }

        public static void AddExecutionSubscription(ExecutionSubscription subscription)
        {
            RefExecutionSubscriptions.Add(subscription);
        }

        private static JObject TxLogToJson(Blockchain.ApplicationExecuted appExec)
        {
            global::System.Diagnostics.Debug.Assert(appExec.Transaction != null);

            var txJson = new JObject
            {
                ["txid"] = appExec.Transaction.Hash.ToString()
            };
            var trigger = new JObject
            {
                ["trigger"] = appExec.Trigger,
                ["vmstate"] = appExec.VMState,
                ["exception"] = appExec.Exception?.GetBaseException().Message,
                ["gasconsumed"] = appExec.GasConsumed.ToString()
            };
            try
            {
                trigger["stack"] = appExec.Stack.Select(q => q.ToJson(Settings.MaxStackSize)).ToArray();
            }
            catch (Exception ex)
            {
                trigger["exception"] = ex.Message;
            }
            trigger["notifications"] = appExec.Notifications.Select(q =>
            {
                var notification = new JObject
                {
                    ["contract"] = q.ScriptHash.ToString(),
                    ["eventname"] = q.EventName
                };
                try
                {
                    notification["state"] = q.State.ToJson();
                }
                catch (InvalidOperationException)
                {
                    notification["state"] = "error: recursive reference";
                }
                return notification;
            }).ToArray();

            txJson["executions"] = new[] { trigger };
            return txJson;
        }

        private static JObject BlockLogToJson(Block block, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
        {
            var blocks = applicationExecutedList.Where(p => p.Transaction is null).ToArray();
            if (blocks.Length <= 0) return null;
            var blockJson = new JObject
            {
                ["blockhash"] = block.Hash.ToString()
            };
            var triggerList = new List<JObject>();
            foreach (var appExec in blocks)
            {
                var trigger = new JObject
                {
                    ["trigger"] = appExec.Trigger,
                    ["vmstate"] = appExec.VMState,
                    ["gasconsumed"] = appExec.GasConsumed.ToString()
                };
                try
                {
                    trigger["stack"] = appExec.Stack.Select(q => q.ToJson(Settings.MaxStackSize)).ToArray();
                }
                catch (Exception ex)
                {
                    trigger["exception"] = ex.Message;
                }
                trigger["notifications"] = appExec.Notifications.Select(q =>
                {
                    JObject notification = new JObject
                    {
                        ["contract"] = q.ScriptHash.ToString(),
                        ["eventname"] = q.EventName
                    };
                    try
                    {
                        notification["state"] = q.State.ToJson();
                    }
                    catch (InvalidOperationException)
                    {
                        notification["state"] = "error: recursive reference";
                    }
                    return notification;
                }).ToArray();
                triggerList.Add(trigger);
            }
            blockJson["executions"] = triggerList.ToArray();
            return blockJson;
        }
    }
}
