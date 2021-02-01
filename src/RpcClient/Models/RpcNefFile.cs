using Neo.IO.Json;
using Neo.SmartContract;
using System;
using System.Linq;

namespace Neo.Network.RPC.Models
{
    class RpcNefFile
    {
        public static NefFile FromJson(JObject json)
        {
            return new NefFile
            {
                Compiler = json["compiler"].AsString(),
                Tokens = ((JArray)json["tokens"]).Select(p => RpcMethodToken.FromJson(p)).ToArray(),
                Script = Convert.FromBase64String(json["script"].AsString()),
                CheckSum = (uint)json["checksum"].AsNumber()
            };
        }
    }
}
