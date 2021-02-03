using Neo.IO.Json;
using System.Numerics;

namespace Neo.Network.RPC.Models
{
    public class RpcUnclaimedGas
    {
        public BigInteger Unclaimed { get; set; }

        public string Address { get; set; }

        public JObject ToJson()
        {
            JObject json = new JObject();
            json["unclaimed"] = Unclaimed.ToString();
            json["address"] = Address;
            return json;
        }

        public static RpcUnclaimedGas FromJson(JObject json)
        {
            return new RpcUnclaimedGas
            {
                Unclaimed = BigInteger.Parse(json["unclaimed"].AsString()),
                Address = json["address"].AsString()
            };
        }
    }
}
