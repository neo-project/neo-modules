using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Array = Neo.VM.Types.Array;

namespace Neo.Plugins.RestServer.Newtonsoft.Json
{
    public class VmArrayJsonConverter : JsonConverter<Array>
    {
        public override Array ReadJson(JsonReader reader, Type objectType, Array existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var t = JToken.Load(reader);
            return RestServerUtility.StackItemFromJToken(t) as Array;
        }

        public override void WriteJson(JsonWriter writer, Array value, JsonSerializer serializer)
        {
            var t = RestServerUtility.StackItemToJToken(value, null);
            t.WriteTo(writer);
        }
    }
}
