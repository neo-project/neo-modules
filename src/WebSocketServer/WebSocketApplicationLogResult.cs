using Neo.Json;
using Neo.SmartContract;

namespace Neo.Plugins
{
    internal class WebSocketApplicationLogResult
    {
        public UInt256 TransactionHash { get; init; }
        public UInt160 ScriptHash { get; init; }
        public string Message { get; init; }

        public static WebSocketApplicationLogResult Create(LogEventArgs logEventArgs) =>
            new()
            {
                TransactionHash = logEventArgs.ScriptContainer?.Hash,
                ScriptHash = logEventArgs.ScriptHash,
                Message = logEventArgs.Message,
            };

        public override string ToString() =>
            $"{ToJson()}";

        public JObject ToJson() =>
            new()
            {
                ["txhash"] = $"{TransactionHash}",
                ["contract"] = $"{ScriptHash}",
                ["message"] = Message,
            };
    }
}
