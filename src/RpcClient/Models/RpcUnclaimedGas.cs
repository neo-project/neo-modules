using Neo.IO.Json;
using Neo.SmartContract.Native;
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
            json["unclaimed"] = new BigDecimal(Unclaimed, NativeContract.GAS.Decimals).ToString();
            json["address"] = Address;
            return json;
        }

        public static RpcUnclaimedGas FromJson(JObject json)
        {
            return new RpcUnclaimedGas
            {
                Unclaimed = BigInteger.Parse(BigDecimal.Parse(json["unclaimed"].AsString(), NativeContract.GAS.Decimals).ToString()),
                Address = json["address"].AsString()
            };
        }
    }
}
