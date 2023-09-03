// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Network.RPC is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System.Linq;
using Neo.Json;
using Neo.Plugins.WebSocketServer.Events;

namespace Neo.Plugins.WebSocketServer.Subscriber;

public partial class WebSocketSubscriber
{
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

    private static JObject CreateEventNotify(WssEventId eventId, JToken @params)
    {
        return new JObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = eventId.ToMethod(),
            ["params"] = @params
        };
    }

}
