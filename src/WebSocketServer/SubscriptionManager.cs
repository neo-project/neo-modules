using System;
using System.Collections.Concurrent;

namespace Neo.Plugins.WebSocketServer;

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
