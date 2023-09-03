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
using System.Threading.Tasks;
using Neo.Json;
using Neo.Plugins.WebSocketServer.Events;
using Neo.Plugins.WebSocketServer.Subscriptions;
using WebSocketSharp;

namespace Neo.Plugins.WebSocketServer.Subscriber;

public partial class WebSocketSubscriber
{
    protected override void OnMessage(MessageEventArgs e)
    {
        var request = JToken.Parse(e.Data);
        ProcessRequestAsync((JObject)request).Wait();
    }


    private JToken Subscribe(JObject @params)
    {
        if (_subscriptions.Count >= WebSocketSubscriber.MaxSubscriptions)
        {
            throw new WssException(-100, "Max subscriptions reached");
        }

        var eventId = @params["eventid"]!.AsString().FromMethod();
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
        var eventId = @params["eventid"]!.AsString().FromMethod();
        var subscriptionId = @params["subscriptionid"]!.AsString();
        switch (eventId)
        {
            case WssEventId.InvalidEventId:
                break;
            case WssEventId.BlockEventId:
                if (!_blockSubscriptions.TryRemove(subscriptionId, out _))
                    return false;
                break;
            case WssEventId.TransactionEventId:
                if (!_txSubscriptions.TryRemove(subscriptionId, out _))
                    return false;
                break;
            case WssEventId.NotificationEventId:
                if (!_notificationSubscriptions.TryRemove(subscriptionId, out _))
                    return false;
                break;
            case WssEventId.ExecutionEventId:
                if (!_executionSubscriptions.TryRemove(subscriptionId, out _))
                    return false;
                break;
            case WssEventId.MissedEventId:
            default:
                throw new ArgumentOutOfRangeException();
        }

        return true;
    }


    private void ClearSubscriptions()
    {
        _blockSubscriptions.Clear();
        _txSubscriptions.Clear();
        _notificationSubscriptions.Clear();
        _executionSubscriptions.Clear();
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
                throw new WssException(-32601, "Method not found");
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
}
