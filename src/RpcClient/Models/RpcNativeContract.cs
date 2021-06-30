using Neo.IO.Json;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using System.Linq;

namespace Neo.Network.RPC.Models
{
    public class RpcNativeContract
    {
        public int Id { get; set; }
        public UInt160 Hash { get; set; }
        public NefFile Nef { get; set; }
        public ContractManifest Manifest { get; set; }
        public uint[] UpdateHistory { get; set; }

        public static RpcNativeContract FromJson(JObject json)
        {
            return new RpcNativeContract
            {
                Id = (int)json["id"].AsNumber(),
                Hash = UInt160.Parse(json["hash"].AsString()),
                Nef = RpcNefFile.FromJson(json["nef"]),
                Manifest = ContractManifest.FromJson(json["manifest"]),
                UpdateHistory = json["updatehistory"].GetArray().Select(u => (uint)u.GetInt32()).ToArray()
            };
        }

        public JObject ToJson()
        {
            return new JObject
            {
                ["id"] = Id,
                ["hash"] = Hash.ToString(),
                ["nef"] = Nef.ToJson(),
                ["manifest"] = Manifest.ToJson(),
                ["updatehistory"] = new JArray(UpdateHistory.Select(u => new JNumber(u)).ToArray())
            };
        }
    }
}
