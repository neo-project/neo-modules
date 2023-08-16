using Neo.VM.Types;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neo.Plugins.RestServer.Newtonsoft.Json
{
    public class InteropInterfaceJsonConverter : JsonConverter<InteropInterface>
    {
        public override InteropInterface ReadJson(JsonReader reader, Type objectType, InteropInterface existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var t = JToken.Load(reader);
            return RestServerUtility.StackItemFromJToken(t) as InteropInterface;
        }

        public override void WriteJson(JsonWriter writer, InteropInterface value, JsonSerializer serializer)
        {
            var t = RestServerUtility.StackItemToJToken(value, null);
            t.WriteTo(writer);
        }
    }
}
