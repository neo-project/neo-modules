using Neo.IO.Json;
using Neo.Wallets;

namespace Neo.Plugins
{
    public class RpcTransferOut
    {
        public UInt160 AssetId { get; set; }

        public UInt160 ScriptHash { get; set; }

        public string Value { get; set; }

        public JObject ToJson()
        {
            return new JObject
            {
                ["asset"] = AssetId.ToString(),
                ["value"] = Value,
                ["address"] = ScriptHash.ToAddress(),
            };
        }

        public static RpcTransferOut FromJson(JObject json)
        {
            return new RpcTransferOut
            {
                AssetId = UInt160.Parse(json["asset"].AsString()),
                Value = json["value"].AsString(),
                ScriptHash = json["address"].AsString().ToScriptHash(),
            };
        }
    }
}
