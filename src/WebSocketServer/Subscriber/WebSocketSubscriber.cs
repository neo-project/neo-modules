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
using Neo.Json;
using Neo.Plugins.WebSocketServer.Subscriptions;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Neo.Plugins.WebSocketServer.Subscriber;

public partial class WebSocketSubscriber : WebSocketBehavior
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

        if (!WebSocketServerPlugin.AddSubscriber(SubscriberId, this)) return;

        WebSocketServerPlugin.BlockEvent += OnBlockEvent;
        WebSocketServerPlugin.TransactionEvent += OnTransactionEvent;
        WebSocketServerPlugin.NotificationEvent += OnNotificationEvent;
        WebSocketServerPlugin.ExecutionEvent += OnExecutionEvent;
    }

    protected override void OnClose(CloseEventArgs e)
    {
        base.OnClose(e);
        ClearSubscriptions();
        WebSocketServerPlugin.RemoveSubscriber(SubscriberId);
    }


    private void NotifySubscriber(JObject response)
    {
        SendAsync(response.ToString(), completed =>
        {
            if (completed)
                Console.WriteLine("Completed sending message.");
        });
    }

    /// <summary>
    /// Close the connection if too many subscribers
    /// </summary>
    public void Close()
    {
        Sessions.CloseSession(ID);
    }
}
