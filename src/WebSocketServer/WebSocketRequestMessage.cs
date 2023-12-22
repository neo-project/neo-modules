using Neo.Json;
using System;

namespace Neo.Plugins
{
    internal class WebSocketRequestMessage
    {
        public Version Version { get; set; }
        public int RequestId { get; set; }
        public string Method { get; set; }
        public JArray Params { get; set; }

        public static WebSocketRequestMessage FromJson(JToken message) =>
            new()
            {
                Version = new(message["version"].AsString()),
                RequestId = checked((int)message["requestid"].AsNumber()),
                Method = message["method"].AsString(),
                Params = (JArray)message["params"],
            };

    }
}
