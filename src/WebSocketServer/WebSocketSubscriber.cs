using System;
using System.Collections.Concurrent;
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

        SendAsync("xxx", completed =>
        {
            if (completed)
                Console.WriteLine("Completed sending message.");
        });
    }

    public void AddSubscription(Subscription subscription)
    {
        if (_subscriptions.Count >= MaxSubscriptions)
        {
            // handle size limit reached
            throw new Network.RPC.RpcException(-100, "Max subscriptions reached");
        }

        subscription.SubscriptionId = Guid.NewGuid().ToString();

        _subscriptions.Add(new WeakReference(subscription));

        switch (subscription)
        {
            case BlockSubscription blockSubscription:
                _blockSubscriptions.TryAdd(subscription.SubscriptionId, blockSubscription);
                WebSocketServerPlugin.AddBlockSubscription(blockSubscription);
                break;
            case TxSubscription txSubscription:
                _txSubscriptions.TryAdd(subscription.SubscriptionId, txSubscription);
                WebSocketServerPlugin.AddTransactionSubscription(txSubscription);
                break;
            case NotificationSubscription notificationSubscription:
                _notificationSubscriptions.TryAdd(subscription.SubscriptionId, notificationSubscription);
                WebSocketServerPlugin.AddNotificationSubscription(notificationSubscription);
                break;
            case ExecutionSubscription executionSubscription:
                _executionSubscriptions.TryAdd(subscription.SubscriptionId, executionSubscription);
                WebSocketServerPlugin.AddExecutionSubscription(executionSubscription);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void OnBlockEvent(EventId eventid, WebSocketEvent @event)
    {
        var blockEvent = (BlockEvent)@event;
        throw new NotImplementedException();
    }

    private void OnExecutionEvent(EventId eventid, WebSocketEvent @event)
    {
        var executionEvent = (ExecutionEvent)@event;
        throw new NotImplementedException();
    }
    private void OnNotificationEvent(EventId eventid, WebSocketEvent @event)
    {
        var notificationEvent = (NotificationEvent)@event;
        throw new NotImplementedException();
    }
    private void OnTransactionEvent(EventId eventid, WebSocketEvent @event)
    {
        var txEvent = (TxEvent)@event;
        throw new NotImplementedException();
    }

    private static JObject CreateErrorResponse(JToken id, int code, string message, JToken data = null)
    {
        JObject response = CreateResponse(id);
        response["error"] = new JObject();
        response["error"]["code"] = code;
        response["error"]["message"] = message;
        if (data != null)
            response["error"]["data"] = data;
        return response;
    }

    private static JObject CreateResponse(JToken id)
    {
        JObject response = new();
        response["jsonrpc"] = "2.0";
        response["id"] = id;
        return response;
    }

    private async Task<JObject> ProcessRequestAsync(JObject request)
    {
        if (!request.ContainsProperty("id")) return null;
        JToken @params = request["params"] ?? new JArray();
        if (!request.ContainsProperty("method") || @params is not JArray)
        {
            return CreateErrorResponse(request["id"], -32600, "Invalid Request");
        }
        JObject response = CreateResponse(request["id"]);
        try
        {
            string method = request["method"].AsString();
            response["result"] = func((JArray)@params) switch
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
}
