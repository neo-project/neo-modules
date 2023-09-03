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
using Neo.Json;
using Neo.Plugins.WebSocketServer.Events;
using Neo.Plugins.WebSocketServer.Filters;
namespace Neo.Plugins.WebSocketServer.Subscriptions;

public abstract class Subscription
{
    public WssEventId WssEvent { get; set; }
    public string? SubscriptionId { get; set; }
    public Filter? Filter { get; set; }

    public abstract Subscription FromJson(JObject json);

    // Helper method to reduce repetition
    protected static T CreateFilterFromJson<T>(JObject json) where T : Filter, new()
    {
        return new T().FromJson((JObject)json["filter"] ?? throw new InvalidOperationException()) as T;
    }
}
