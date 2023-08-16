using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Neo.Plugins.RestServer.Newtonsoft.Json
{
    public class UInt256JsonConverter : JsonConverter<UInt256>
    {
        public override UInt256 ReadJson(JsonReader reader, Type objectType, UInt256 existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            //var o = JObject.Load(reader);
            //return UInt256.Parse(o["value"].ToObject<string>());
            return UInt256.Parse(reader.ReadAsString());
        }

        public override void WriteJson(JsonWriter writer, UInt256 value, JsonSerializer serializer)
        {
            //var o = new JObject()
            //{
            //    new JProperty("type", "Hash256"),
            //    new JProperty("value", value.ToString()),
            //};
            //o.WriteTo(writer);
            writer.WriteValue(value.ToString());
        }
    }
}
