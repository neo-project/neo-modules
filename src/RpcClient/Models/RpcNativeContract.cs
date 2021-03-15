using Neo.IO.Json;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;

namespace Neo.Network.RPC.Models
{
    public class RpcNativeContract
    {
        public int Id { get; set; }
        public UInt160 Hash { get; set; }
        public NefFile Nef { get; set; }
        public ContractManifest Manifest { get; set; }
        public uint ActiveBlockIndex { get; set; }

        public static RpcNativeContract FromJson(JObject json)
        {
            return new RpcNativeContract
            {
                Id = (int)json["id"].AsNumber(),
                Hash = UInt160.Parse(json["hash"].AsString()),
                Nef = RpcNefFile.FromJson(json["nef"]),
                Manifest = ContractManifest.FromJson(json["manifest"]),
                ActiveBlockIndex = (uint)(json["activeblockindex"]?.AsNumber() ?? 0)
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
                ["activeblockindex"] = ActiveBlockIndex
            };
        }
    }
}
