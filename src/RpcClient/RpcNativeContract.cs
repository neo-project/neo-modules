using Neo.IO.Json;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using System;

namespace Neo.Network.RPC.Models
{
    public class RpcNativeContract
    {
        public string Name { get; set; }
        public NefFile Nef { get; set; }
        public byte[] Script { get; set; }
        public UInt160 Hash { get; set; }
        public int Id { get; set; }
        public ContractManifest Manifest { get; set; }
        public uint ActiveBlockIndex { get; set; }

        public static RpcNativeContract FromJson(JObject json)
        {
            return new RpcNativeContract
            {
                Name = json["name"].AsString(),
                Nef = RpcNefFile.FromJson(json["nef"]),
                Script = Convert.FromBase64String(json["script"].AsString()),
                Hash = UInt160.Parse(json["hash"].AsString()),
                Id = (int)json["id"].AsNumber(),
                Manifest = ContractManifest.FromJson(json["manifest"]),
                ActiveBlockIndex = (uint)json["activeblockindex"].AsNumber()
            };
        }
    }
}
