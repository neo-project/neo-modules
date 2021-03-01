using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract.Native;
using System.Linq;

namespace Neo.Plugins
{
    static class Utility
    {
        public static JObject BlockToJson(Block block, ProtocolSettings settings)
        {
            JObject json = block.ToJson(settings);
            json["tx"] = block.Transactions.Select(p => TransactionToJson(p, settings)).ToArray();
            return json;
        }

        public static JObject TransactionToJson(Transaction tx, ProtocolSettings settings)
        {
            JObject json = tx.ToJson(settings);
            json["sysfee"] = tx.SystemFee.ToString();
            json["netfee"] = tx.NetworkFee.ToString();
            return json;
        }

        public static JObject NativeContractToJson(this NativeContract contract, ProtocolSettings settings)
        {
            return new JObject
            {
                ["id"] = contract.Id,
                ["hash"] = contract.Hash.ToString(),
                ["nef"] = contract.Nef.ToJson(),
                ["manifest"] = contract.Manifest.ToJson(),
                ["updatehistory"] = settings.NativeUpdateHistory[contract.Name].Select(p => (JObject)p).ToArray()
            };
        }
    }
}
