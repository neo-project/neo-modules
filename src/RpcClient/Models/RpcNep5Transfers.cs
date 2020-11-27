using Neo.IO.Json;
using Neo.SmartContract;
using Neo.Wallets;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Neo.Network.RPC.Models
{
    public class RpcNep17Transfers
    {
        public UInt160 UserScriptHash { get; set; }

        public List<RpcNep17Transfer> Sent { get; set; }

        public List<RpcNep17Transfer> Received { get; set; }

        public JObject ToJson()
        {
            JObject json = new JObject();
            json["sent"] = Sent.Select(p => p.ToJson()).ToArray();
            json["received"] = Received.Select(p => p.ToJson()).ToArray();
            json["address"] = UserScriptHash.ToAddress();
            return json;
        }

        public static RpcNep17Transfers FromJson(JObject json)
        {
            RpcNep17Transfers transfers = new RpcNep17Transfers
            {
                Sent = ((JArray)json["sent"]).Select(p => RpcNep17Transfer.FromJson(p)).ToList(),
                Received = ((JArray)json["received"]).Select(p => RpcNep17Transfer.FromJson(p)).ToList(),
                UserScriptHash = json["address"].ToScriptHash()
            };
            return transfers;
        }
    }

    public class RpcNep17Transfer
    {
        public ulong TimestampMS { get; set; }

        public UInt160 AssetHash { get; set; }

        public UInt160 UserScriptHash { get; set; }

        public BigInteger Amount { get; set; }

        public uint BlockIndex { get; set; }

        public ushort TransferNotifyIndex { get; set; }

        public UInt256 TxHash { get; set; }

        public JObject ToJson()
        {
            JObject json = new JObject();
            json["timestamp"] = TimestampMS;
            json["assethash"] = AssetHash.ToString();
            json["transferaddress"] = UserScriptHash?.ToAddress();
            json["amount"] = Amount.ToString();
            json["blockindex"] = BlockIndex;
            json["transfernotifyindex"] = TransferNotifyIndex;
            json["txhash"] = TxHash.ToString();
            return json;
        }

        public static RpcNep17Transfer FromJson(JObject json)
        {
            return new RpcNep17Transfer
            {
                TimestampMS = (ulong)json["timestamp"].AsNumber(),
                AssetHash = json["assethash"].ToScriptHash(),
                UserScriptHash = json["transferaddress"]?.ToScriptHash(),
                Amount = BigInteger.Parse(json["amount"].AsString()),
                BlockIndex = (uint)json["blockindex"].AsNumber(),
                TransferNotifyIndex = (ushort)json["transfernotifyindex"].AsNumber(),
                TxHash = UInt256.Parse(json["txhash"].AsString())
            };
        }
    }
}
