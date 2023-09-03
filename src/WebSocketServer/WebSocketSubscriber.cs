using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Neo.Json;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Neo.Plugins.WebSocketServer;

public class WebSocketSubscriber : WebSocketBehavior
{
    private const int MaxSubscriptions = 16;
    private const int NotificationBufSize = 1024;
    private string SubscriberId { get; set; }

    private readonly Dictionary<string, Func<JObject, object>> _methods;

    private readonly ConcurrentDictionary<string, BlockSubscription> _blockSubscriptions = new();
    private readonly ConcurrentDictionary<string, TxSubscription> _txSubscriptions = new();
    private readonly ConcurrentDictionary<string, NotificationSubscription> _notificationSubscriptions = new();
    private readonly ConcurrentDictionary<string, ExecutionSubscription> _executionSubscriptions = new();

    private readonly ConcurrentBag<WeakReference> _subscriptions = new();

    public WebSocketSubscriber()
    {
        _methods = new Dictionary<string, Func<JObject, object>>
        {
            { "subscribe", Subscribe },
            { "unsubscribe", UnSubscribe }
        };
    }

    protected override void OnOpen()
    {
        base.OnOpen();
        SubscriberId = Guid.NewGuid().ToString();

        WebSocketServerPlugin.BlockEvent += OnBlockEvent;
        WebSocketServerPlugin.TransactionEvent += OnTransactionEvent;
        WebSocketServerPlugin.NotificationEvent += OnNotificationEvent;
        WebSocketServerPlugin.ExecutionEvent += OnExecutionEvent;
    }

    protected override void OnClose(CloseEventArgs e)
    {
        base.OnClose(e);
        ClearSubscriptions();
    }

    private void ClearSubscriptions()
    {
        _blockSubscriptions.Clear();
        _txSubscriptions.Clear();
        _notificationSubscriptions.Clear();
        _executionSubscriptions.Clear();
    }

    protected override void OnMessage(MessageEventArgs e)
    {
        var request = JToken.Parse(e.Data);
        ProcessRequestAsync((JObject)request).Wait();
    }

    private void OnBlockEvent(WebSocketEvent @event)
    {
        if (_blockSubscriptions.Any(p => p.Value.Matches(@event.Data, WssEventId.BlockEventId)))
            NotifySubscriber(CreateEventNotify(WssEventId.BlockEventId, @event.Data));
    }

    private void OnNotificationEvent(WebSocketEvent @event)
    {
        var notifications = (JArray)(JToken)@event.Data;
        foreach (var notification in notifications)
        {
            if (_notificationSubscriptions.Any(p => p.Value.Matches((JObject)notification, WssEventId.NotificationEventId)))
            {
                NotifySubscriber(CreateEventNotify(WssEventId.NotificationEventId, notification));
            }
        }
    }
    private void OnTransactionEvent(WebSocketEvent @event)
    {
        if (_txSubscriptions.Any(p => p.Value.Matches(@event.Data, WssEventId.TransactionEventId)))
            NotifySubscriber(CreateEventNotify(WssEventId.TransactionEventId, @event.Data));
    }

    private void OnExecutionEvent(WebSocketEvent @event)
    {
        if (_executionSubscriptions.Any(p => p.Value.Matches(@event.Data, WssEventId.ExecutionEventId)))
            NotifySubscriber(CreateEventNotify(WssEventId.ExecutionEventId, @event.Data));
    }
    private JToken Subscribe(JObject @params)
    {
        if (_subscriptions.Count >= MaxSubscriptions)
        {
            throw new Network.RPC.RpcException(-100, "Max subscriptions reached");
        }

        var eventId = @params["eventid"]!.AsString().ParseEventId();
        Subscription subscription = null;
        var subscriptionId = Guid.NewGuid().ToString();
        switch (eventId)
        {
            case WssEventId.InvalidEventId:
                break;
            case WssEventId.BlockEventId:
                subscription = new BlockSubscription().FromJson(@params);
                _blockSubscriptions.TryAdd(subscriptionId, (BlockSubscription)subscription);
                WebSocketServerPlugin.AddBlockSubscription((BlockSubscription)subscription);
                break;
            case WssEventId.TransactionEventId:
                subscription = new TxSubscription().FromJson(@params);
                _txSubscriptions.TryAdd(subscriptionId, (TxSubscription)subscription);
                WebSocketServerPlugin.AddTransactionSubscription((TxSubscription)subscription);
                break;
            case WssEventId.NotificationEventId:
                subscription = new NotificationSubscription().FromJson(@params);
                _notificationSubscriptions.TryAdd(subscriptionId, (NotificationSubscription)subscription);
                WebSocketServerPlugin.AddNotificationSubscription((NotificationSubscription)subscription);
                break;
            case WssEventId.ExecutionEventId:
                subscription = new ExecutionSubscription().FromJson(@params);
                _executionSubscriptions.TryAdd(subscriptionId, (ExecutionSubscription)subscription);
                WebSocketServerPlugin.AddExecutionSubscription((ExecutionSubscription)subscription);
                break;
            case WssEventId.MissedEventId:
            default:
                throw new ArgumentOutOfRangeException();
        }

        _subscriptions.Add(new WeakReference(subscription));
        return subscriptionId;
    }

    private JToken UnSubscribe(JObject @params)
    {
        var eventId = @params["eventid"]!.AsString().ParseEventId();
        var subscriptionId = @params["subscriptionid"]!.AsString();
        switch (eventId)
        {
            case WssEventId.InvalidEventId:
                break;
            case WssEventId.BlockEventId:
                if (!_blockSubscriptions.TryRemove(subscriptionId, out var blockSubscription))
                    return false;
                break;
            case WssEventId.TransactionEventId:
                if (!_txSubscriptions.TryRemove(subscriptionId, out var txSubscription))
                    return false;
                break;
            case WssEventId.NotificationEventId:
                if (!_notificationSubscriptions.TryRemove(subscriptionId, out var notificationSubscription))
                    return false;
                break;
            case WssEventId.ExecutionEventId:
                if (!_executionSubscriptions.TryRemove(subscriptionId, out var executionSubscription))
                    return false;
                break;
            case WssEventId.MissedEventId:
            default:
                throw new ArgumentOutOfRangeException();
        }

        return true;
    }

    private static JObject CreateErrorResponse(JToken id, int code, string message, JToken data = null)
    {
        var response = CreateResponse(id);
        response["error"] = new JObject();
        response["error"]["code"] = code;
        response["error"]["message"] = message;
        if (data != null)
            response["error"]["data"] = data;
        return response;
    }

    private static JObject CreateResponse(JToken id)
    {
        return new JObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id
        };
    }
    private static JObject CreateEventNotify(WssEventId eventId, JToken @params)
    {
        return new JObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = eventId.ToMethod(),
            ["params"] = @params
        };
    }

    private async Task ProcessRequestAsync(JObject request)
    {
        if (!request.ContainsProperty("id")) return;
        JToken @params = request["params"] ?? new JArray();
        if (!request.ContainsProperty("method") || @params is not JArray)
        {
            var res = CreateErrorResponse(request["id"], -32600, "Invalid Request");
            NotifySubscriber(res);
        }

        var response = CreateResponse(request["id"]);
        try
        {
            var method = request["method"]?.AsString();
            if (!_methods.TryGetValue(method ?? throw new InvalidOperationException(), out var func))
                throw new RpcException(-32601, "Method not found");
            response["result"] = func((JObject)@params) switch
            {
                JToken result => result,
                Task<JToken> task => await task,
                _ => throw new NotSupportedException()
            };

            NotifySubscriber(response);
        }

        catch (FormatException)
        {
            var res = CreateErrorResponse(request["id"], -32602, "Invalid params");
            NotifySubscriber(res);
        }
        catch (IndexOutOfRangeException)
        {
            var res = CreateErrorResponse(request["id"], -32602, "Invalid params");
            NotifySubscriber(res);
        }
        catch (Exception ex)
        {
#if DEBUG
            var res = CreateErrorResponse(request["id"], ex.HResult, ex.Message, ex.StackTrace);
            NotifySubscriber(res);
#else
            var res = CreateErrorResponse(request["id"], ex.HResult, ex.Message);
            NotifySubscriber(res);
#endif
        }

    }
    private void NotifySubscriber(JObject response)
    {
        SendAsync(response.ToString(), completed =>
        {
            if (completed)
                Console.WriteLine("Completed sending message.");
        });
    }
}
