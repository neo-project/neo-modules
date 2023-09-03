#nullable enable
using System;
using Neo.Json;
namespace Neo.Plugins.WebSocketServer;

public abstract class Subscription
{
    public required EventId Event { get; set; }

    // Server assigns each subscription a unique ID
    public string? SubscriptionId { get; set; }

    // without filter means get all notifications of this type
    public Filter? Filter { get; set; }

    public abstract Subscription FromJson(JObject json);
}

public class BlockSubscription : Subscription
{
    public ulong Height { get; set; }

    public override Subscription FromJson(JObject json)
    {
        return new BlockSubscription
        {
            Height = (ulong)json["height"]!.GetInt32(),
            Filter = new TxFilter().FromJson((JObject)json["filter"]! ?? throw new InvalidOperationException()),
            Event = EventId.BlockEventId
        };
    }
}

public class TxSubscription : Subscription
{
    public UInt256? TxId { get; set; }
    public override Subscription FromJson(JObject json)
    {
        return new TxSubscription
        {
            TxId = UInt256.Parse(json["txid"]
                ?.GetString()),
            Filter = new TxFilter().FromJson((JObject)json["filter"]! ?? throw new InvalidOperationException()),
            Event = EventId.TransactionEventId,
        };
    }
}

public class NotificationSubscription : Subscription
{
    public override Subscription FromJson(JObject json)
    {
        return new NotificationSubscription
        {
            Filter = new NotificationFilter().FromJson((JObject)json["filter"]! ?? throw new InvalidOperationException()),
            Event = EventId.NotificationEventId,
        };
    }
}

public class ExecutionSubscription : Subscription
{
    public UInt256? TxId { get; set; }

    public override Subscription FromJson(JObject json)
    {
        return new ExecutionSubscription
        {
            TxId = UInt256.Parse(json["txid"]
                ?.GetString()),
            Filter = new ExecutionFilter().FromJson((JObject)json["filter"]! ?? throw new InvalidOperationException()),
            Event = EventId.ExecutionEventId,
        };
    }
}


public abstract class Filter
{
    public abstract Filter FromJson(JObject json);
}

public class BlockFilter : Filter
{
    public int? Primary { get; set; }
    public uint? Since { get; set; }
    public uint? Till { get; set; }

    public override Filter FromJson(JObject json)
    {
        return new BlockFilter
        {
            Primary = json["primary"]?.GetInt32(),
            Since = (uint?)json["since"]?.GetInt32(),
            Till = (uint?)json["till"]?.GetInt32()
        };
    }
}

public class TxFilter : Filter
{
    public UInt160? Sender { get; set; }
    public UInt160? Signer { get; set; }

    public override Filter FromJson(JObject json)
    {
        return new TxFilter
        {
            Sender = UInt160.Parse(json["sender"]?.GetString()),
            Signer = UInt160.Parse(json["signer"]?.GetString()),
        };

    }
}

public class NotificationFilter : Filter
{
    public UInt160? Contract { get; set; }
    public string? Name { get; set; }
    public override Filter FromJson(JObject json)
    {
        return new NotificationFilter
        {
            Contract = UInt160.Parse(json["contract"]?.GetString()),
            Name = json["name"]?.GetString()
        };
    }
}

public class ExecutionFilter : Filter
{
    public string? State { get; set; }
    public UInt256? Container { get; set; }

    public override Filter FromJson(JObject json)
    {
        return new ExecutionFilter
        {
            State = json["state"]?.GetString(),
            Container = UInt256.Parse(json["container"]?.GetString())
        };
    }
}
