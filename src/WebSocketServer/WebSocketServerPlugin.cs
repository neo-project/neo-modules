using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Neo.ConsoleService;
using Neo.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Persistence;

namespace Neo.Plugins.WebSocketServer;

public class WebSocketServerPlugin : Plugin
{
    public override string Name => "NeoWebSocketServer";
    public override string Description => "Enables WebSocket notifications for the node";

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
        Blockchain.Committed += OnCommitted;
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
        Blockchain.Committed -= OnCommitted;
        _server?.Stop();
        base.Dispose();
    }

    [ConsoleCommand("start wss", Category = "wss", Description = "Open Web Socket Server")]
    private void OnStart()
    {
        if (_server is { IsListening: true }) return;

        var s = _settings.Servers.FirstOrDefault(p => p.Network == _system.Settings.Network);
        if (s == null) return;

        var useSsl = !string.IsNullOrEmpty(s.SslCert) && !string.IsNullOrEmpty(s.SslCertPassword);
        _server = new WebSocketSharp.Server.WebSocketServer(s.BindAddress, s.Port, useSsl);
        if (useSsl)
        {
            _server.SslConfiguration.ServerCertificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(s.SslCert, s.SslCertPassword);
        }
        _server.AddWebSocketService("/", () => new WebSocketSubscriber());
        _server.Start();
    }

    [ConsoleCommand("close wss", Category = "wss", Description = "Close Web Socket Server")]
    private void OnClose()
    {
        if (_server is { IsListening: true }) _server.Stop();
        ConsoleHelper.Info("Web Socket Server closed");
    }

    private void OnCommitting(NeoSystem system, Block block, DataCache snapshot, IReadOnlyList<Blockchain.ApplicationExecuted> applicationExecutedList)
    {
        if (system.Settings.Network != ProtocolSettings.Default.Network) return;

        if (!RefBlockSubscriptions.IsEmpty)
            NotifySubscribers(WssEventId.BlockEventId, LogReader.BlockLogToJson(block, applicationExecutedList));

        //processing log for transactions
        foreach (var appExec in applicationExecutedList.Where(p => p.Transaction != null))
        {
            if (!RefTxSubscriptions.IsEmpty)
                NotifySubscribers(WssEventId.TransactionEventId, appExec.Transaction.ToJson(ProtocolSettings.Default));
            if (!RefNotificationSubscriptions.IsEmpty)
                NotifySubscribers(WssEventId.ExecutionEventId, LogReader.TxLogToJson(appExec));
            if (!RefExecutionSubscriptions.IsEmpty)
                NotifySubscribers(WssEventId.NotificationEventId, LogReader.TxLogToJson(appExec));
        }
    }


    private static void OnCommitted(NeoSystem system, Block block)
    {
    }

    private static void NotifySubscribers(WssEventId wssEventId, JObject jObject)
    {
        switch (wssEventId)
        {
            case WssEventId.BlockEventId:
                if (RefBlockSubscriptions.IsEmpty) return;
                var blockEvent = new BlockEvent
                {
                    WssEvent = WssEventId.BlockEventId,
                    Data = jObject,
                    Height = jObject["index"]!.GetInt32()
                };
                BlockEvent?.Invoke(blockEvent);
                break;
            case WssEventId.TransactionEventId:
                if (RefTxSubscriptions.IsEmpty) return;
                var txEvent = new TxEvent()
                {
                    WssEvent = WssEventId.TransactionEventId,
                    Data = jObject,
                    Container = UInt256.Parse(jObject["txid"]?.AsString())
                };
                TransactionEvent?.Invoke(txEvent);
                break;
            case WssEventId.NotificationEventId:
                if (RefNotificationSubscriptions.IsEmpty) return;
                var notificationEvent = new NotificationEvent()
                {
                    WssEvent = WssEventId.NotificationEventId,
                    Contract = UInt160.Parse(jObject["executions"]?["contract"]?.AsString()),
                    Data = (JObject)jObject["executions"]?["notifications"],
                };
                NotificationEvent?.Invoke(notificationEvent);
                break;
            case WssEventId.ExecutionEventId:
                if (RefExecutionSubscriptions.IsEmpty) return;
                var executionEvent = new ExecutionEvent
                {
                    WssEvent = WssEventId.ExecutionEventId,
                    Container = UInt256.Parse(jObject["txid"]?.AsString()),
                    Data = jObject,
                    VmState = jObject["executions"]?["vmstate"]?.AsString(),
                };
                ExecutionEvent?.Invoke(executionEvent);
                break;
            case WssEventId.InvalidEventId:
                break;
            case WssEventId.MissedEventId:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(wssEventId), wssEventId, null);
        }
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
