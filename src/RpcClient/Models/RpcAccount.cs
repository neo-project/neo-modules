using Neo.IO.Json;

namespace Neo.Network.RPC.Models
{
    public class RpcAccount
    {
        public string Address { get; set; }

        public bool HasKey { get; set; }

        public string Label { get; set; }

        public bool WatchOnly { get; set; }

        public JObject ToJson()
        {
            return new JObject
            {
                ["address"] = Address,
                ["has_key"] = HasKey,
                ["label"] = Label,
                ["watch_only"] = WatchOnly
            };
        }

        public static RpcAccount FromJson(JObject json)
        {
            return new RpcAccount
            {
                Address = json["address"].AsString(),
                HasKey = json["has_key"].AsBoolean(),
                Label = json["label"]?.AsString(),
                WatchOnly = json["watch_only"].AsBoolean(),
            };
        }
    }
}
