using Neo.IO.Json;
using System.Numerics;

namespace Neo.Network.RPC.Models
{
    public class RpcValidator
    {
        public string PublicKey { get; set; }

        public BigInteger Votes { get; set; }

        public bool Active { get; set; }

        public JObject ToJson()
        {
            JObject json = new JObject();
            json["publickey"] = PublicKey;
            json["votes"] = Votes.ToString();
            json["active"] = Active;
            return json;
        }

        public static RpcValidator FromJson(JObject json)
        {
            return new RpcValidator
            {
                PublicKey = json["publickey"].AsString(),
                Votes = BigInteger.Parse(json["votes"].AsString()),
                Active = json["active"].AsBoolean()
            };
        }
    }
}
