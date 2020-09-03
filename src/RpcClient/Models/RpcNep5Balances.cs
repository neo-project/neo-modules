using Neo.IO.Json;
using Neo.Wallets;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Neo.Network.RPC.Models
{
    public class RpcNep5Balances
    {
        public UInt160 UserScriptHash { get; set; }

        public List<RpcNep5Balance> Balances { get; set; }

        public JObject ToJson()
        {
            JObject json = new JObject();
            json["balance"] = Balances.Select(p => p.ToJson()).ToArray();
            json["address"] = UserScriptHash.ToAddress();
            return json;
        }

        public static RpcNep5Balances FromJson(JObject json)
        {
            RpcNep5Balances nep5Balance = new RpcNep5Balances
            {
                Balances = ((JArray)json["balance"]).Select(p => RpcNep5Balance.FromJson(p)).ToList(),
                UserScriptHash = json["address"].ToScriptHash()
            };
            return nep5Balance;
        }
    }

    public class RpcNep5Balance
    {
        public UInt160 AssetHash { get; set; }

        public BigInteger Amount { get; set; }

        public uint LastUpdatedBlock { get; set; }

        public JObject ToJson()
        {
            JObject json = new JObject();
            json["assethash"] = AssetHash.ToString();
            json["amount"] = Amount.ToString();
            json["lastupdatedblock"] = LastUpdatedBlock;
            return json;
        }

        public static RpcNep5Balance FromJson(JObject json)
        {
            RpcNep5Balance balance = new RpcNep5Balance
            {
                AssetHash = json["assethash"].ToScriptHash(),
                Amount = BigInteger.Parse(json["amount"].AsString()),
                LastUpdatedBlock = (uint)json["lastupdatedblock"].AsNumber()
            };
            return balance;
        }
    }
}
