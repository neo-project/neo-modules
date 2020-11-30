using Neo.IO.Json;
using Neo.SmartContract.Native;

namespace Neo.Network.RPC.Models
{
    public class RpcUnclaimedGas
    {
        public BigDecimal Unclaimed { get; set; }

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
                Unclaimed = BigDecimal.Parse(json["unclaimed"].AsString(), NativeContract.GAS.Decimals),
                Address = json["address"].AsString()
            };
        }
    }
}
