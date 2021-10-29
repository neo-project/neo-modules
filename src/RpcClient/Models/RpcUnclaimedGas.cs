using Neo.IO.Json;

namespace Neo.Network.RPC.Models
{
    public class RpcUnclaimedGas
    {
        public long Unclaimed { get; set; }

        public string Address { get; set; }

        public JObject ToJson()
        {
            JObject json = new();
            json["unclaimed"] = Unclaimed.ToString();
            json["address"] = Address;
            return json;
        }

        public static RpcUnclaimedGas FromJson(JObject json)
        {
            return new RpcUnclaimedGas
            {
                Unclaimed = long.Parse(json["unclaimed"].AsString()),
                Address = json["address"].AsString()
            };
        }
    }
}
