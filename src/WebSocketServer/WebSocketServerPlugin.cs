using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Neo.ConsoleService;
using Neo.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;

namespace Neo.Plugins.WebSocketServer
{
    public class WebSocketServerPlugin : Plugin
    {
        public override string Name => "NeoWebSocketServer";
        public override string Description => "Enables WebSocket notifications for the node";

        private const string INDEX_KEY = "index";
        private const string TXID_KEY = "txid";
        private const string EXECUTIONS_KEY = "executions";

        private Settings _settings;
        private static WebSocketSharp.Server.WebSocketServer _server;
        private NeoSystem _system;

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
            _server.AddWebSocketService("/", () => new WebSocketSubscriber());
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
                NotifyBlockEvent(LogReader.BlockLogToJson(block, applicationExecutedList));

            foreach (var appExec in applicationExecutedList.Where(p => p.Transaction != null))
            {
                if (!RefTxSubscriptions.IsEmpty)
                    NotifyTransactionEvent(appExec.Transaction.ToJson(ProtocolSettings.Default));
                if (!RefNotificationSubscriptions.IsEmpty)
                    NotifyNotificationEvent(LogReader.TxLogToJson(appExec));
                if (!RefExecutionSubscriptions.IsEmpty)
                    NotifyExecutionEvent(LogReader.TxLogToJson(appExec));
            }
        }

        private static void NotifyBlockEvent(JObject jObject)
        {
            var blockEvent = new BlockEvent
            {
                WssEvent = WssEventId.BlockEventId,
                Data = jObject,
                Height = jObject[INDEX_KEY]!.GetInt32()
            };
            BlockEvent?.Invoke(blockEvent);
        }

        private static void NotifyTransactionEvent(JObject jObject)
        {
            var txEvent = new TxEvent
            {
                WssEvent = WssEventId.TransactionEventId,
                Data = jObject,
                Container = UInt256.Parse(jObject[TXID_KEY]?.AsString())
            };
            TransactionEvent?.Invoke(txEvent);
        }

        private static void NotifyNotificationEvent(JObject jObject)
        {
            var notificationEvent = new NotificationEvent
            {
                WssEvent = WssEventId.NotificationEventId,
                Contract = UInt160.Parse(jObject[EXECUTIONS_KEY]?["contract"]?.AsString()),
                Data = (JObject)jObject[EXECUTIONS_KEY]?["notifications"],
            };
            NotificationEvent?.Invoke(notificationEvent);
        }

        private static void NotifyExecutionEvent(JObject jObject)
        {
            var executionEvent = new ExecutionEvent
            {
                WssEvent = WssEventId.ExecutionEventId,
                Container = UInt256.Parse(jObject[TXID_KEY]?.AsString()),
                Data = jObject,
                VmState = jObject[EXECUTIONS_KEY]?["vmstate"]?.AsString(),
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
    }
}
