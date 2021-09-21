using System;
using System.Linq;
using Neo.IO.Json;

namespace Neo.Network.RPC.Models
{
    public class RpcFoundStates
    {
        public bool Truncated;
        public (byte[] key, byte[] value)[] Results;
        public byte[] FirstProof;
        public byte[] LastProof;

        public static RpcFoundStates FromJson(JObject json)
        {
            return new RpcFoundStates
            {
                Truncated = json["truncated"].AsBoolean(),
                Results = ((JArray)json["results"])
                    .Select(j => (
                        Convert.FromBase64String(j["key"].AsString()),
                        Convert.FromBase64String(j["value"].AsString())
                    ))
                    .ToArray(),
                FirstProof = ProofFromJson(json["firstProof"]),
                LastProof = ProofFromJson(json["lastProof"]),
            };
        }

        static byte[] ProofFromJson(JObject json)
            => json == null ? null : Convert.FromBase64String(json.AsString());
    }
}
