using Neo.Json;
using System;

namespace Neo.Plugins
{
    internal class WebSocketErrorResult
    {
        public int Code { get; init; }
        public string Message { get; init; } = string.Empty;
#if DEBUG
        public string? StackTrace { get; init; }
#endif

        public static WebSocketErrorResult Create(Exception exception) =>
            new()
            {
                Code = exception.HResult,
                Message = exception.Message.Trim(),
#if DEBUG
                StackTrace = exception.StackTrace?.Trim()
#endif
            };

        public static WebSocketErrorResult Create(int code, string message) =>
            new()
            {
                Code = code,
                Message = message.Trim(),
            };

        public override string ToString() =>
            $"{ToJson()}";

        public JToken ToJson() =>
            new JObject()
            {
                ["code"] = Code,
                ["message"] = Message,
#if DEBUG
                ["stackTrace"] = StackTrace,
#endif
            };
    }
}
