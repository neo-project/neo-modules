using Neo.IO.Json;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;

namespace Neo.Network.RPC.Models
{
    public class RpcContractState
    {
        public ContractState ContractState { get; set; }

        public JObject ToJson()
        {
            return ContractState.ToJson();
        }

        public static RpcContractState FromJson(JObject json)
        {
            return new RpcContractState
            {
                ContractState = new ContractState
                {
                    Id = (int)json["id"].AsNumber(),
                    UpdateCounter = (ushort)json["updatecounter"].AsNumber(),
                    Hash = UInt160.Parse(json["hash"].AsString()),
                    Nef = RpcNefFile.FromJson(json["nef"]),
                    Manifest = ContractManifest.FromJson(json["manifest"])
                }
            };
        }
    }
}
