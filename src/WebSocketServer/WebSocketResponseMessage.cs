using Neo.Json;
using System;
using System.Text;

namespace Neo.Plugins
{
    internal class WebSocketResponseMessage
    {
        public Version Version { get; init; } = new("1.0");
        public Guid RequestId { get; set; }
        public byte EventId { get; init; }
        public JToken Result { get; init; }

        internal static WebSocketResponseMessage Create(Guid requestId, JToken result, WebSocketResponseMessageEvent eventId) =>
            new()
            {
                EventId = (byte)eventId,
                RequestId = requestId,
                Result = result,
            };

        internal static WebSocketResponseMessage Create(Guid requestId, JToken result, byte eventId) =>
            new()
            {
                EventId = eventId,
                RequestId = requestId,
                Result = result,
            };

        public JToken ToJson() =>
            new JObject()
            {
                ["version"] = $"{Version}",
                ["requestid"] = $"{RequestId}",
                ["eventid"] = EventId,
                ["result"] = Result,
            };

        public static WebSocketResponseMessage FromJson(JToken message) =>
            new()
            {
                Version = new(message["version"].AsString()),
                RequestId = Guid.Parse(message["requestid"].AsString()),
                EventId = (byte)message["eventid"].AsNumber(),
                Result = message["result"],
            };

        public override string ToString() =>
            $"{ToJson()}";

        public byte[] ToArray() =>
            Encoding.UTF8.GetBytes(ToString());

        public bool Equals(WebSocketResponseMessage other) =>
            other != null && other.EventId == EventId && other.RequestId == RequestId &&
            other.Version == Version && other.Result == Result;

        public override bool Equals(object obj) =>
            Equals(obj as WebSocketResponseMessage);

        public override int GetHashCode() =>
            HashCode.Combine(this, Version, EventId, RequestId, Result);
    }
}
