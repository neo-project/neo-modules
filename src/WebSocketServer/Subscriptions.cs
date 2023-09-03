#nullable enable
using System;
using Neo.Json;
namespace Neo.Plugins.WebSocketServer;

public abstract class Subscription
{
    public WssEventId WssEvent { get; set; }

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
        Height = (ulong)json["height"]!.GetInt32();
        Filter = new TxFilter().FromJson((JObject)json["filter"]! ?? throw new InvalidOperationException());
        WssEvent = WssEventId.BlockEventId;
        return this;
    }
}

public class TxSubscription : Subscription
{
    public UInt256? TxId { get; set; }
    public override Subscription FromJson(JObject json)
    {
        TxId = UInt256.Parse(json["txid"]?.GetString());
        Filter = new TxFilter().FromJson((JObject)json["filter"]! ?? throw new InvalidOperationException());
        WssEvent = WssEventId.TransactionEventId;
        return this;
    }
}

public class NotificationSubscription : Subscription
{
    public override Subscription FromJson(JObject json)
    {
        Filter = new NotificationFilter().FromJson((JObject)json["filter"]! ?? throw new InvalidOperationException());
        WssEvent = WssEventId.NotificationEventId;
        return this;
    }
}

public class ExecutionSubscription : Subscription
{
    public UInt256? TxId { get; set; }

    public override Subscription FromJson(JObject json)
    {
        TxId = UInt256.Parse(json["txid"]?.GetString());
        Filter = new ExecutionFilter().FromJson((JObject)json["filter"]! ?? throw new InvalidOperationException());
        WssEvent = WssEventId.ExecutionEventId;
        return this;
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
        Primary = json["primary"]?.GetInt32();
        Since = (uint?)json["since"]?.GetInt32();
        Till = (uint?)json["till"]?.GetInt32();
        return this;
    }
}

public class TxFilter : Filter
{
    public UInt160? Sender { get; set; }
    public UInt160? Signer { get; set; }

    public override Filter FromJson(JObject json)
    {
        Sender = UInt160.Parse(json["sender"]?.GetString());
        Signer = UInt160.Parse(json["signer"]?.GetString());
        return this;

    }
}

public class NotificationFilter : Filter
{
    public UInt160? Contract { get; set; }
    public string? Name { get; set; }
    public override Filter FromJson(JObject json)
    {
        Contract = UInt160.Parse(json["contract"]?.GetString());
        Name = json["name"]?.GetString();
        return this;
    }
}

public class ExecutionFilter : Filter
{
    public string? State { get; set; }
    public UInt256? Container { get; set; }

    public override Filter FromJson(JObject json)
    {
        State = json["state"]?.GetString();
        Container = UInt256.Parse(json["container"]?.GetString());
        return this;
    }
}
