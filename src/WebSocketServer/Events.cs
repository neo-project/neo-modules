using System;
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

public static class EventIdExtensions
{
    public static string ToString(this WssEventId e)
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
