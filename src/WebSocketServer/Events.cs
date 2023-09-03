using System;
using System.Text.Json;
using Neo.Json;

namespace Neo.Plugins.WebSocketServer;

public enum EventId : byte
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
    public static string ToString(this EventId e)
    {
        return e switch
        {
            EventId.BlockEventId => "block_added",
            EventId.TransactionEventId => "transaction_added",
            EventId.NotificationEventId => "notification_from_execution",
            EventId.ExecutionEventId => "transaction_executed",
            EventId.MissedEventId => "event_missed",
            _ => "unknown"
        };
    }

    public static EventId Parse(this string s)
    {
        return s switch
        {
            "block_added" => EventId.BlockEventId,
            "transaction_added" => EventId.TransactionEventId,
            "notification_from_execution" => EventId.NotificationEventId,
            "transaction_executed" => EventId.ExecutionEventId,
            "event_missed" => EventId.MissedEventId,
            _ => throw new ArgumentException("Invalid event ID string"),
        };
    }
}

public abstract class WebSocketEvent
{
    public EventId Event { get; set; }
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
