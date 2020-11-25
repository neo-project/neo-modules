using Neo.IO.Json;

namespace Neo.Network.RPC.Models
{
    public class RpcValidateAddressResult
    {
        public string Address { get; set; }

        public bool IsValid { get; set; }

        public JObject ToJson()
        {
            JObject json = new JObject();
            json["address"] = Address;
            json["isvalid"] = IsValid;
            return json;
        }

        public static RpcValidateAddressResult FromJson(JObject json)
        {
            return new RpcValidateAddressResult
            {
                Address = json["address"].AsString(),
                IsValid = json["isvalid"].AsBoolean()
            };
        }
    }
}
