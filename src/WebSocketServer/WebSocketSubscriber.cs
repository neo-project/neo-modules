using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Neo.Json;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Neo.Plugins.WebSocketServer;

public class WebSocketSubscriber : WebSocketBehavior
{
    const int MaxSubscriptions = 16;
    const int NotificationBufSize = 1024;
    private string SubscriberId { get; set; }

    private readonly Dictionary<string, Func<JObject, object>> _methods = new();

    private readonly ConcurrentDictionary<string, BlockSubscription> _blockSubscriptions = new();
    private readonly ConcurrentDictionary<string, TxSubscription> _txSubscriptions = new();
    private readonly ConcurrentDictionary<string, NotificationSubscription> _notificationSubscriptions = new();
    private readonly ConcurrentDictionary<string, ExecutionSubscription> _executionSubscriptions = new();

    private readonly ConcurrentBag<WeakReference> _subscriptions = new();

    protected override void OnOpen()
    {
        base.OnOpen();
        SubscriberId = Guid.NewGuid().ToString();

        WebSocketServerPlugin.BlockEvent += OnBlockEvent;
        WebSocketServerPlugin.TransactionEvent += OnTransactionEvent;
        WebSocketServerPlugin.NotificationEvent += OnNotificationEvent;
        WebSocketServerPlugin.ExecutionEvent += OnExecutionEvent;

        _methods.Add("subscribe", Subscribe);
        _methods.Add("unsubscribe", UnSubscribe);
    }

    protected override void OnClose(CloseEventArgs e)
    {
        base.OnClose(e);
        _blockSubscriptions.Clear();
        _txSubscriptions.Clear();
        _notificationSubscriptions.Clear();
        _executionSubscriptions.Clear();
    }

    /// <summary>
    /// Receive message from the client
    /// </summary>
    /// <param name="e"></param>
    protected override void OnMessage(MessageEventArgs e)
    {
        var request = JToken.Parse(e.Data);
        if (request is JObject json) return;

    }

    private void OnBlockEvent(WebSocketEvent @event)
    {
        var blockEvent = (BlockEvent)@event;
        throw new NotImplementedException();
    }

    private void OnExecutionEvent(WebSocketEvent @event)
    {
        var executionEvent = (ExecutionEvent)@event;
        throw new NotImplementedException();
    }
    private void OnNotificationEvent(WebSocketEvent @event)
    {
        var notificationEvent = (NotificationEvent)@event;
        throw new NotImplementedException();
    }
    private void OnTransactionEvent(WebSocketEvent @event)
    {
        var txEvent = (TxEvent)@event;
        throw new NotImplementedException();
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
                _blockSubscriptions.TryRemove(subscriptionId, out BlockSubscription blockSubscription);
                break;
            case WssEventId.TransactionEventId:
                _txSubscriptions.TryRemove(subscriptionId, out TxSubscription txSubscription);
                break;
            case WssEventId.NotificationEventId:
                _notificationSubscriptions.TryRemove(subscriptionId, out NotificationSubscription notificationSubscription);
                break;
            case WssEventId.ExecutionEventId:
                _executionSubscriptions.TryRemove(subscriptionId, out ExecutionSubscription executionSubscription);
                break;
            case WssEventId.MissedEventId:
            default:
                throw new ArgumentOutOfRangeException();
        }

        // _subscriptions.Add(new WeakReference(subscription));
        return subscriptionId;

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

    private async Task<JObject> ProcessRequestAsync(JObject request)
    {
        if (!request.ContainsProperty("id")) return null;
        JToken @params = request["params"] ?? new JArray();
        if (!request.ContainsProperty("method") || @params is not JArray)
        {
            return CreateErrorResponse(request["id"], -32600, "Invalid Request");
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
            return response;
        }
        catch (FormatException)
        {
            return CreateErrorResponse(request["id"], -32602, "Invalid params");
        }
        catch (IndexOutOfRangeException)
        {
            return CreateErrorResponse(request["id"], -32602, "Invalid params");
        }
        catch (Exception ex)
        {
#if DEBUG
            return CreateErrorResponse(request["id"], ex.HResult, ex.Message, ex.StackTrace);
#else
            return CreateErrorResponse(request["id"], ex.HResult, ex.Message);
#endif
        }

    }
    private void Response()
    {
        SendAsync("xxx", completed =>
        {
            if (completed)
                Console.WriteLine("Completed sending message.");
        });
    }
}
