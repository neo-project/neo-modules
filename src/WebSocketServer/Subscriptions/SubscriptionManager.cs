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
namespace Neo.Plugins.WebSocketServer.Subscriptions;

public class SubscriptionManager<T> where T : Subscription
{
    private readonly ConcurrentDictionary<string, WeakReference<T>> _references = new();

    public void Add(T target)
    {
        var weakReference = new WeakReference<T>(target);
        _references.TryAdd(target.SubscriptionId, weakReference);
    }

    private void Cleanup()
    {
        foreach (var kvp in _references)
        {
            if (!kvp.Value.TryGetTarget(out _))
            {
                _references.TryRemove(kvp.Key, out _);
            }
        }
    }

    public bool IsEmpty
    {
        get
        {
            Cleanup();
            return _references.IsEmpty;
        }
    }
}
