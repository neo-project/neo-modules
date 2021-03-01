using Neo.IO.Json;
using Neo.Wallets;

namespace Neo.Network.RPC.Models
{
    public class RpcTransferOut
    {
        public UInt160 Asset { get; set; }

        public UInt160 ScriptHash { get; set; }

        public string Value { get; set; }

        public JObject ToJson(ProtocolSettings protocolSettings)
        {
            return new JObject
            {
                ["asset"] = Asset.ToString(),
                ["value"] = Value,
                ["address"] = ScriptHash.ToAddress(protocolSettings.AddressVersion),
            };
        }

        public static RpcTransferOut FromJson(JObject json, ProtocolSettings protocolSettings)
        {
            return new RpcTransferOut
            {
                Asset = json["asset"].ToScriptHash(protocolSettings),
                Value = json["value"].AsString(),
                ScriptHash = json["address"].ToScriptHash(protocolSettings),
            };
        }
    }
}
