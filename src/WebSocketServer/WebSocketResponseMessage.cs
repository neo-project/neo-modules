using Neo.Json;
using System;
using System.Text;

namespace Neo.Plugins
{
    internal class WebSocketResponseMessage : IEquatable<WebSocketResponseMessage>
    {
        public Version Version { get; private init; } = new("1.0");
        public int RequestId { get; private init; }
        public byte EventId { get; private init; }
        public JToken? Result { get; private init; }

        internal static WebSocketResponseMessage Create(int requestId, JToken result, WebSocketResponseMessageEvent eventId) =>
            new()
            {
                EventId = (byte)eventId,
                RequestId = requestId,
                Result = result,
            };

        internal static WebSocketResponseMessage Create(int requestId, JToken result, byte eventId) =>
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
                ["requestid"] = RequestId,
                ["eventid"] = EventId,
                ["result"] = Result,
            };

        public static WebSocketResponseMessage FromJson(JToken message) =>
            new()
            {
                Version = message["version"] == null ? new("1.0") : new(message["version"]!.AsString()),
                RequestId = message["requestid"] == null ? 0 : checked((int)message["requestid"]!.AsNumber()),
                EventId = message["eventid"] == null ? (byte)0 : unchecked((byte)message["eventid"]!.AsNumber()),
                Result = message["result"],
            };

        public override string ToString() =>
            $"{ToJson()}";

        public byte[] ToArray() =>
            Encoding.UTF8.GetBytes(ToString());

        public bool Equals(WebSocketResponseMessage? other) =>
            other != null && other.EventId == EventId && other.RequestId == RequestId &&
            other.Version == Version && other.Result == Result;

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as WebSocketResponseMessage);
        }

        public override int GetHashCode() =>
            HashCode.Combine(this, Version, EventId, RequestId, Result);
    }
}
