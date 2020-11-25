using Neo.IO.Json;
using Neo.Ledger;
using Neo.SmartContract.Manifest;
using System;

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
                Script = Convert.FromBase64String(json["script"].AsString()),
                Manifest = ContractManifest.FromJson(json["manifest"])
            }
        };
    }
}
