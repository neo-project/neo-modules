using Newtonsoft.Json;

namespace Neo.Plugins.RestServer.Newtonsoft.Json
{
    public class UInt160JsonConverter : JsonConverter<UInt160>
    {
        public override UInt160 ReadJson(JsonReader reader, Type objectType, UInt160 existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            //var o = JObject.Load(reader);
            //return UInt160.Parse(o["value"].ToObject<string>());
            return UInt160.Parse(reader.ReadAsString());
        }

        public override void WriteJson(JsonWriter writer, UInt160 value, JsonSerializer serializer)
        {
            //var o = new JObject()
            //{
            //    new JProperty("type", "Hash160"),
            //    new JProperty("value", value.ToString()),
            //};
            //o.WriteTo(writer);
            writer.WriteValue(value.ToString());
        }
    }
}
