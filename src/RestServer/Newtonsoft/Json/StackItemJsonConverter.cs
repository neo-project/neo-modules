using Neo.VM.Types;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neo.Plugins.RestServer.Newtonsoft.Json
{
    public class StackItemJsonConverter : JsonConverter<StackItem>
    {
        public override StackItem ReadJson(JsonReader reader, Type objectType, StackItem existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var t = JObject.Load(reader);
            return RestServerUtility.StackItemFromJToken(t);
        }

        public override void WriteJson(JsonWriter writer, StackItem value, JsonSerializer serializer)
        {
            var t = RestServerUtility.StackItemToJToken(value, null);
            t.WriteTo(writer);
        }

        
    }
}
