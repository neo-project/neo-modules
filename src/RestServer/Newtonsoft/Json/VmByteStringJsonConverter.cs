using Neo.VM.Types;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neo.Plugins.RestServer.Newtonsoft.Json
{
    public class VmByteStringJsonConverter : JsonConverter<ByteString>
    {
        public override ByteString ReadJson(JsonReader reader, Type objectType, ByteString existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var t = JToken.ReadFrom(reader);
            return RestServerUtility.StackItemFromJToken(t) as ByteString;
        }

        public override void WriteJson(JsonWriter writer, ByteString value, JsonSerializer serializer)
        {
            var t = RestServerUtility.StackItemToJToken(value, null);
            t.WriteTo(writer);
        }
    }
}
