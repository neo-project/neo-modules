using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Neo.Cryptography.ECC;

namespace Neo.Plugins.RestServer.Newtonsoft.Json
{
    public class ECPointJsonConverter : JsonConverter<ECPoint>
    {
        public override ECPoint ReadJson(JsonReader reader, Type objectType, ECPoint existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var o = JObject.Load(reader);
            return ECPoint.Parse(o["value"].ToObject<string>(), ECCurve.Secp256r1);
        }

        public override void WriteJson(JsonWriter writer, ECPoint value, JsonSerializer serializer)
        {
            var o = new JObject()
            {
                new JProperty("type", "PublicKey"),
                new JProperty("value", value.ToString()),
            };
            o.WriteTo(writer);
        }
    }
}
