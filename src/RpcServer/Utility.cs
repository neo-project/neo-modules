using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Neo.Plugins
{
    public static class Utility
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
            json["sysfee"] = new BigDecimal(tx.SystemFee, NativeContract.GAS.Decimals).ToString();
            json["netfee"] = new BigDecimal(tx.NetworkFee, NativeContract.GAS.Decimals).ToString();
            return json;
        }
    }
}
