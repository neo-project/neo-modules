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
                ["haskey"] = HasKey,
                ["label"] = Label,
                ["watchonly"] = WatchOnly
            };
        }

        public static RpcAccount FromJson(JObject json)
        {
            return new RpcAccount
            {
                Address = json["address"].AsString(),
                HasKey = json["haskey"].AsBoolean(),
                Label = json["label"]?.AsString(),
                WatchOnly = json["watchonly"].AsBoolean(),
            };
        }
    }
}
