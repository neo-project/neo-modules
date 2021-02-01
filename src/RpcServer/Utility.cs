using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract.Native;
using System.Linq;
using System.Numerics;

namespace Neo.Plugins
{
    static class Utility
    {
        public static JObject BlockToJson(Block block)
        {
            JObject json = block.ToJson();
            json["tx"] = block.Transactions.Select(p => TransactionToJson(p)).ToArray();
            return json;
        }

        public static JObject TransactionToJson(Transaction tx)
        {
            JObject json = tx.ToJson();
            json["sysfee"] = new BigDecimal(new BigInteger(tx.SystemFee), NativeContract.GAS.Decimals).ToString();
            json["netfee"] = new BigDecimal(new BigInteger(tx.NetworkFee), NativeContract.GAS.Decimals).ToString();
            return json;
        }

        public static JObject NativeContractToJson(this NativeContract contract)
        {
            return new JObject
            {
                ["id"] = contract.Id,
                ["hash"] = contract.Hash.ToString(),
                ["nef"] = contract.Nef.ToJson(),
                ["manifest"] = contract.Manifest.ToJson(),
                ["activeblockindex"] = contract.ActiveBlockIndex
            };
        }
    }
}
