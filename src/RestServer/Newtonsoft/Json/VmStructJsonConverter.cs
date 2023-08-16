using Neo.VM.Types;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neo.Plugins.RestServer.Newtonsoft.Json
{
    public class VmStructJsonConverter : JsonConverter<Struct>
    {
        public override Struct ReadJson(JsonReader reader, Type objectType, Struct existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var t = JToken.Load(reader);
            return RestServerUtility.StackItemFromJToken(t) as Struct;
        }

        public override void WriteJson(JsonWriter writer, Struct value, JsonSerializer serializer)
        {
            var t = RestServerUtility.StackItemToJToken(value, null);
            t.WriteTo(writer);
        }
    }
}
