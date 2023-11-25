using Neo.Json;

namespace Neo.Plugins
{
    internal class WebSocketUtilityLogResult
    {
        public string SourceName { get; init; }
        public LogLevel LogLevel { get; init; }
        public object Message { get; init; }

        public static WebSocketUtilityLogResult Create(string sourceName, LogLevel level, object message) =>
            new()
            {
                SourceName = sourceName,
                LogLevel = level,
                Message = message
            };

        public override string ToString() =>
            $"{ToJson()}";

        public JObject ToJson() =>
            new()
            {
                ["source"] = SourceName,
                ["level"] = $"{LogLevel}",
                ["message"] = $"{Message}",
            };
    }
}
