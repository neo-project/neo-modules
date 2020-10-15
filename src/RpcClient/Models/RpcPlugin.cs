using Neo.IO.Json;
using System.Linq;

namespace Neo.Network.RPC.Models
{
    public class RpcPlugin
    {
        public string Name { get; set; }

        public string Version { get; set; }

        public string[] Interfaces { get; set; }

        public JObject ToJson()
        {
            JObject json = new JObject();
            json["name"] = Name;
            json["version"] = Version;
            json["interfaces"] = new JArray(Interfaces.Select(p => (JObject)p));
            return json;
        }

        public static RpcPlugin FromJson(JObject json)
        {
            return new RpcPlugin
            {
                Name = json["name"].AsString(),
                Version = json["version"].AsString(),
                Interfaces = ((JArray)json["interfaces"]).Select(p => p.AsString()).ToArray()
            };
        }
    }
}
