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
using System.Linq;
using Neo.Json;
using Neo.Plugins.WebSocketServer.Filters;
using Neo.Plugins.WebSocketServer.Subscriptions;
namespace Neo.Plugins.WebSocketServer.Events;

// Enum representing WebSocket server event IDs
public enum WssEventId : byte
{
    InvalidEventId = 0,
    BlockEventId,
    TransactionEventId,
    NotificationEventId,
    ExecutionEventId,
    MissedEventId = 255
}

public static class EventExtensions
{
    // Convert event ID to its string representation
    public static string ToMethod(this WssEventId e)
    {
        return e switch
        {
            WssEventId.BlockEventId => "block_added",
            WssEventId.TransactionEventId => "transaction_added",
            WssEventId.NotificationEventId => "notification_from_execution",
            WssEventId.ExecutionEventId => "transaction_executed",
            WssEventId.MissedEventId => "event_missed",
            _ => "unknown"
        };
    }

    // Convert string to its corresponding event ID
    public static WssEventId FromMethod(this string s)
    {
        return s switch
        {
            "block_added" => WssEventId.BlockEventId,
            "transaction_added" => WssEventId.TransactionEventId,
            "notification_from_execution" => WssEventId.NotificationEventId,
            "transaction_executed" => WssEventId.ExecutionEventId,
            "event_missed" => WssEventId.MissedEventId,
            _ => throw new ArgumentException($"Invalid event ID string: {s}")
        };
    }

    // Check if a subscription matches a WebSocket event
    public static bool Matches(this Subscription subscription, JObject wssEvent, WssEventId eventId)
    {
        if (subscription.WssEvent != eventId) return false;

        switch (subscription.WssEvent)
        {
            case WssEventId.BlockEventId:
                return HandleBlockEvent(subscription, wssEvent);
            case WssEventId.TransactionEventId:
                return HandleTransactionEvent(subscription, wssEvent);
            case WssEventId.NotificationEventId:
                return HandleNotificationEvent(subscription, wssEvent);
            case WssEventId.ExecutionEventId:
                return HandleExecutionEvent(subscription, wssEvent);
            default:
                return false;
        }
    }

    private static bool HandleBlockEvent(Subscription subscription, JObject wssEvent)
    {
        var blockFilter = (BlockFilter)subscription.Filter;
        var blockIndex = wssEvent["index"]!.GetInt32();
        var primaryIndex = wssEvent["primary"]!.GetInt32();

        return (blockFilter.Primary == null || blockFilter.Primary == primaryIndex) &&
            (blockFilter.Since == null || blockFilter.Since <= blockIndex) &&
            (blockFilter.Till == null || blockIndex <= blockFilter.Till);
    }

    private static bool HandleTransactionEvent(Subscription subscription, JObject wssEvent)
    {
        var transactionFilter = (TransactionFilter)subscription.Filter;
        var txSender = UInt160.Parse(wssEvent["sender"]!.GetString());

        if (transactionFilter.Signer == null) return txSender.Equals(transactionFilter.Sender);

        var txSigners = (JArray)wssEvent["signers"];
        return txSigners.Any(signer => UInt160.Parse(signer["account"]!.GetString()).Equals(transactionFilter.Signer));
    }

    private static bool HandleNotificationEvent(Subscription subscription, JObject wssEvent)
    {
        var notificationFilter = (NotificationFilter)subscription.Filter;
        var notificationContract = UInt160.Parse(wssEvent["contract"]!.GetString());
        var notificationName = wssEvent["eventname"]!.GetString();

        return (notificationFilter.Contract == null || notificationContract.Equals(notificationFilter.Contract)) &&
            (notificationFilter.Name == null || notificationName.Equals(notificationFilter.Name));
    }

    private static bool HandleExecutionEvent(Subscription subscription, JObject wssEvent)
    {
        var executionFilter = (ExecutionFilter)subscription.Filter;
        var execResultVmState = wssEvent["executions"]!["vmstate"]!.GetString();
        var execResultContainer = UInt256.Parse(wssEvent["txid"]!.GetString());

        return (executionFilter.State == null || execResultVmState == executionFilter.State) &&
            (executionFilter.Container == null || execResultContainer.Equals(executionFilter.Container));
    }
}
