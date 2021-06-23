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

        public JObject ToJson(ProtocolSettings protocolSettings)
        {
            JObject json = new();
            json["balance"] = Balances.Select(p => p.ToJson()).ToArray();
            json["address"] = UserScriptHash.ToAddress(protocolSettings.AddressVersion);
            return json;
        }

        public static RpcNep17Balances FromJson(JObject json, ProtocolSettings protocolSettings)
        {
            RpcNep17Balances nep17Balance = new()
            {
                Balances = ((JArray)json["balance"]).Select(p => RpcNep17Balance.FromJson(p, protocolSettings)).ToList(),
                UserScriptHash = json["address"].ToScriptHash(protocolSettings)
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
            JObject json = new();
            json["assethash"] = AssetHash.ToString();
            json["amount"] = Amount.ToString();
            json["lastupdatedblock"] = LastUpdatedBlock;
            return json;
        }

        public static RpcNep17Balance FromJson(JObject json, ProtocolSettings protocolSettings)
        {
            RpcNep17Balance balance = new()
            {
                AssetHash = json["assethash"].ToScriptHash(protocolSettings),
                Amount = BigInteger.Parse(json["amount"].AsString()),
                LastUpdatedBlock = (uint)json["lastupdatedblock"].AsNumber()
            };
            return balance;
        }
    }
}
