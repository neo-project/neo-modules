using Neo.IO.Json;
using Neo.SmartContract;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neo.Network.RPC.Models
{
    class RpcNefFile
    {
        public NefFile NefFile { get; set; }

        public JObject ToJson()
        {
            return NefFile.ToJson();
        }

        public static NefFile FromJson(JObject json)
        {
            return new NefFile
            {
                Compiler = json["compiler"].AsString(),
                Version = json["version"].AsString(),
                Tokens = ((JArray)json["tokens"]).Select(p => RpcMethodToken.FromJson(p)).ToArray(),
                Script = Convert.FromBase64String(json["script"].AsString()),
                CheckSum = (uint)json["checksum"].AsNumber()
            };
        }
    }
}
