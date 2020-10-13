using Neo.IO.Json;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Network.RPC.Models
{
    public class RpcRawMemPool
    {
        public uint Height { get; set; }

        public List<UInt256> Verified { get; set; }

        public List<UInt256> UnVerified { get; set; }

        public JObject ToJson()
        {
            JObject json = new JObject();
            json["height"] = Height;
            json["verified"] = new JArray(Verified.Select(p => (JObject)p.ToString()));
            json["unverified"] = new JArray(UnVerified.Select(p => (JObject)p.ToString()));
            return json;
        }

        public static RpcRawMemPool FromJson(JObject json)
        {
            RpcRawMemPool rawMemPool = new RpcRawMemPool();
            rawMemPool.Height = uint.Parse(json["height"].AsString());
            rawMemPool.Verified = ((JArray)json["verified"]).Select(p => UInt256.Parse(p.AsString())).ToList();
            rawMemPool.UnVerified = ((JArray)json["unverified"]).Select(p => UInt256.Parse(p.AsString())).ToList();
            return rawMemPool;
        }
    }
}
