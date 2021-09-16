using System;
using System.Linq;
using Neo.IO.Json;

namespace Neo.Network.RPC.Models
{
    public class RpcFoundStates
    {
        public bool Truncated;
        public (byte[] key, byte[] value)[] States;

        public static RpcFoundStates FromJson(JObject json)
        {
            return new RpcFoundStates
            {
                Truncated = json["truncated"].AsBoolean(),
                States = ((JArray)json["array"])
                    .Select(j => (
                        Convert.FromBase64String(j["key"].AsString()),
                        Convert.FromBase64String(j["value"].AsString())
                    ))
                    .ToArray()
            };
        }
    }
}
