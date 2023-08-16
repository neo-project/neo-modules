using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Boolean =  Neo.VM.Types.Boolean;

namespace Neo.Plugins.RestServer.Newtonsoft.Json
{
    public class VmBooleanJsonConverter : JsonConverter<Boolean>
    {
        public override Boolean ReadJson(JsonReader reader, Type objectType, Boolean existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var t = JToken.ReadFrom(reader);
            return RestServerUtility.StackItemFromJToken(t) as Boolean;
        }

        public override void WriteJson(JsonWriter writer, Boolean value, JsonSerializer serializer)
        {
            var t = RestServerUtility.StackItemToJToken(value, null);
            t.WriteTo(writer);
        }
    }
}
