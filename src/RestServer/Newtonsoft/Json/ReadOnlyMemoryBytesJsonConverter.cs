using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neo.Plugins.RestServer.Newtonsoft.Json
{
    public class ReadOnlyMemoryBytesJsonConverter : JsonConverter<ReadOnlyMemory<byte>>
    {
        public override ReadOnlyMemory<byte> ReadJson(JsonReader reader, Type objectType, ReadOnlyMemory<byte> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var o = JToken.Load(reader);
            return Convert.FromBase64String(o.ToObject<string>());
        }

        public override void WriteJson(JsonWriter writer, ReadOnlyMemory<byte> value, JsonSerializer serializer)
        {
            writer.WriteValue(Convert.ToBase64String(value.Span));
        }
    }
}
