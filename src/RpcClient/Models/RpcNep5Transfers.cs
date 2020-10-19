using Neo.IO.Json;
using Neo.SmartContract;
using Neo.Wallets;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Neo.Network.RPC.Models
{
    public class RpcNep5Transfers
    {
        public UInt160 UserScriptHash { get; set; }

        public List<RpcNep5Transfer> Sent { get; set; }

        public List<RpcNep5Transfer> Received { get; set; }

        public JObject ToJson()
        {
            JObject json = new JObject();
            json["sent"] = Sent.Select(p => p.ToJson()).ToArray();
            json["received"] = Received.Select(p => p.ToJson()).ToArray();
            json["address"] = UserScriptHash.ToAddress();
            return json;
        }

        public static RpcNep5Transfers FromJson(JObject json)
        {
            RpcNep5Transfers transfers = new RpcNep5Transfers
            {
                Sent = ((JArray)json["sent"]).Select(p => RpcNep5Transfer.FromJson(p)).ToList(),
                Received = ((JArray)json["received"]).Select(p => RpcNep5Transfer.FromJson(p)).ToList(),
                UserScriptHash = json["address"].ToScriptHash()
            };
            return transfers;
        }
    }

    public class RpcNep5Transfer
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

        public static RpcNep5Transfer FromJson(JObject json)
        {
            RpcNep5Transfer transfer = new RpcNep5Transfer();
            transfer.TimestampMS = (ulong)json["timestamp"].AsNumber();
            transfer.AssetHash = json["assethash"].ToScriptHash();
            transfer.UserScriptHash = json["transferaddress"]?.ToScriptHash();
            transfer.Amount = BigInteger.Parse(json["amount"].AsString());
            transfer.BlockIndex = (uint)json["blockindex"].AsNumber();
            transfer.TransferNotifyIndex = (ushort)json["transfernotifyindex"].AsNumber();
            transfer.TxHash = UInt256.Parse(json["txhash"].AsString());
            return transfer;
        }
    }
}
