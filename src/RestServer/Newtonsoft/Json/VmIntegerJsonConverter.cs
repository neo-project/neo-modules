using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Integer = Neo.VM.Types.Integer;

namespace Neo.Plugins.RestServer.Newtonsoft.Json
{
    public class VmIntegerJsonConverter : JsonConverter<Integer>
    {
        public override Integer ReadJson(JsonReader reader, Type objectType, Integer existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var t = JToken.ReadFrom(reader);
            return RestServerUtility.StackItemFromJToken(t) as Integer;
        }

        public override void WriteJson(JsonWriter writer, Integer value, JsonSerializer serializer)
        {
            var t = RestServerUtility.StackItemToJToken(value, null);
            t.WriteTo(writer);
        }
    }
}
