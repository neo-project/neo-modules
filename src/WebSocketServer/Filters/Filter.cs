#nullable enable

namespace Neo.Plugins.WebSocketServer.Filters;

public record BlockFilter(int? Primary, uint? Since, uint? Till);

public record TxFilter(UInt160? Sender, UInt160? Signer);

public record NotificationFilter(UInt160? Contract, string? Name);

public record ExecutionFilter(string? State, UInt256? Container);

public interface IComparator
{
    WssEventId WssEventId { get; }
    object Filter { get; }
}

public interface IContainer
{
    WssEventId WssEventId { get; }
    object EventPayload { get; }
}
