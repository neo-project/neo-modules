using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Buffer =  Neo.VM.Types.Buffer;

namespace Neo.Plugins.RestServer.Newtonsoft.Json
{
    public class VmBufferJsonConverter : JsonConverter<Buffer>
    {
        public override Buffer ReadJson(JsonReader reader, Type objectType, Buffer existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var t = JToken.ReadFrom(reader);
            return RestServerUtility.StackItemFromJToken(t) as Buffer;
        }

        public override void WriteJson(JsonWriter writer, Buffer value, JsonSerializer serializer)
        {
            var t = RestServerUtility.StackItemToJToken(value, null);
            t.WriteTo(writer);
        }
    }
}
