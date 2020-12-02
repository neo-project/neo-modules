using Neo.IO.Json;
using Neo.Wallets;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Neo.Network.RPC.Models
{
    public class RpcNep17Balances
    {
        public UInt160 UserScriptHash { get; set; }

        public List<RpcNep17Balance> Balances { get; set; }

        public JObject ToJson()
        {
            JObject json = new JObject();
            json["balance"] = Balances.Select(p => p.ToJson()).ToArray();
            json["address"] = UserScriptHash.ToAddress();
            return json;
        }

        public static RpcNep17Balances FromJson(JObject json)
        {
            RpcNep17Balances nep17Balance = new RpcNep17Balances
            {
                Balances = ((JArray)json["balance"]).Select(p => RpcNep17Balance.FromJson(p)).ToList(),
                UserScriptHash = json["address"].ToScriptHash()
            };
            return nep17Balance;
        }
    }

    public class RpcNep17Balance
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

        public static RpcNep17Balance FromJson(JObject json)
        {
            RpcNep17Balance balance = new RpcNep17Balance
            {
                AssetHash = json["assethash"].ToScriptHash(),
                Amount = BigInteger.Parse(json["amount"].AsString()),
                LastUpdatedBlock = (uint)json["lastupdatedblock"].AsNumber()
            };
            return balance;
        }
    }
}
