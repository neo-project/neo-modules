using Neo.Json;
using System;

namespace Neo.Plugins
{
    internal class WebSocketRequestMessage
    {
        public Version? Version { get; set; }
        public int RequestId { get; set; }
        public string? Method { get; set; }
        public JArray? Params { get; set; }

        public static WebSocketRequestMessage FromJson(JToken? message) =>
            new()
            {
                Version = message?["version"] == null ? null : new(message["version"]!.AsString()),
                RequestId = message?["requestid"] == null ? 0 : checked((int)message["requestid"]!.AsNumber()),
                Method = message?["method"]?.AsString(),
                Params = message?["params"] == null ? null : (JArray)message["params"]!,
            };

    }
}
