using System;
using System.Linq;
using Neo.Json;

namespace Neo.Plugins.WebSocketServer;

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

    public static WssEventId ParseEventId(this string s)
    {
        return s switch
        {
            "block_added" => WssEventId.BlockEventId,
            "transaction_added" => WssEventId.TransactionEventId,
            "notification_from_execution" => WssEventId.NotificationEventId,
            "transaction_executed" => WssEventId.ExecutionEventId,
            "event_missed" => WssEventId.MissedEventId,
            _ => throw new ArgumentException("Invalid event ID string"),
        };
    }

    public static bool Matches(this Subscription subscription, JObject wssEvent, WssEventId eventId)
    {
        if (subscription.WssEvent != eventId) return false;
        switch (subscription.WssEvent)
        {
            case WssEventId.BlockEventId:
                {
                    var blockFilter = (BlockFilter)subscription.Filter;
                    var blockIndex = wssEvent["index"]!.GetInt32();
                    var primaryIndex = wssEvent["primary"]!.GetInt32();
                    var primaryOk = blockFilter!.Primary == null || blockFilter.Primary == primaryIndex;
                    var sinceOk = blockFilter.Since == null || blockFilter.Since <= blockIndex;
                    var tillOk = blockFilter.Till == null || blockIndex <= blockFilter.Till;
                    return primaryOk && sinceOk && tillOk;
                }
            case WssEventId.TransactionEventId:
                {
                    var txFilter = (TxFilter)subscription.Filter;
                    var txSender = UInt160.Parse(wssEvent["sender"]!.GetString());
                    var senderOk = txFilter!.Sender == null || txSender.Equals(txFilter.Sender);
                    if (txFilter.Signer == null) return senderOk;
                    var txSigners = (JArray)wssEvent["signers"];
                    var signerOk = txSigners!.Any(signer => UInt160.Parse(signer["account"]!.GetString()).Equals(txFilter.Signer));
                    return senderOk && signerOk;
                }
            case WssEventId.NotificationEventId:
                {
                    var notificationFilter = (NotificationFilter)subscription.Filter;
                    var notificationContract = UInt160.Parse(wssEvent["contract"]!.GetString());
                    var notificationName = wssEvent["eventname"]!.GetString();
                    var hashOk = notificationFilter!.Contract == null || notificationContract.Equals(notificationFilter.Contract);
                    var nameOk = notificationFilter.Name == null || notificationName.Equals(notificationFilter.Name);
                    return hashOk && nameOk;
                }
            case WssEventId.ExecutionEventId:
                {
                    var executionFilter = (ExecutionFilter)subscription.Filter;
                    var execResultVmState = wssEvent["executions"]!["vmstate"]!.GetString();
                    var execResultContainer = UInt256.Parse(wssEvent["txid"]!.GetString());
                    var stateOk = executionFilter!.State == null || execResultVmState == executionFilter.State;
                    var containerOk = executionFilter.Container == null || execResultContainer.Equals(executionFilter.Container);
                    return stateOk && containerOk;
                }
            default:
                return false;
        }

    }
}

public abstract class WebSocketEvent
{
    public WssEventId WssEvent { get; set; }
    public JObject Data { get; set; }
}

public class BlockEvent : WebSocketEvent
{
    public required int Height { get; set; }
}

public class TxEvent : WebSocketEvent
{
    public required UInt256 Container { get; set; }
}

public class NotificationEvent : WebSocketEvent
{
    public required UInt160 Contract { get; set; }
    public string Name { get; set; }
}

public class ExecutionEvent : WebSocketEvent
{
    public required string VmState { get; set; }
    public required UInt256 Container { get; set; }
}
