using Neo.Json;
using System;

namespace Neo.Plugins
{
    internal class WebSocketErrorResponseMessage
    {
        public int Code { get; init; }
        public string Message { get; init; }
#if DEBUG
        public string StackTrace { get; init; }
#endif

        public static WebSocketErrorResponseMessage Create(Exception exception) =>
            new()
            {
                Code = exception.HResult,
                Message = exception.Message.Trim(),
#if DEBUG
                StackTrace = exception.StackTrace?.Trim()
#endif
            };

        public static WebSocketErrorResponseMessage Create(int code, string message) =>
            new()
            {
                Code = code,
                Message = message.Trim(),
            };

#if DEBUG
        public static WebSocketErrorResponseMessage Create(int code, string message, string stackTrace) =>
            new()
            {
                Code = code,
                Message = message.Trim(),
                StackTrace = stackTrace.Trim(),
            };
#endif

        public override string ToString() =>
            $"{ToJson()}";

        public JObject ToJson() =>
            new()
            {
                ["code"] = Code,
                ["message"] = Message,
#if DEBUG
                ["stackTrace"] = StackTrace,
#endif
            };
    }
}
