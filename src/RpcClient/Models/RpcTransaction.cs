// Copyright (C) 2015-2021 The Neo Project.
//
// The Neo.Network.RPC is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
using Neo.VM;

namespace Neo.Network.RPC.Models
{
    public class RpcTransaction
    {
        public Transaction Transaction { get; set; }

        public UInt256 BlockHash { get; set; }

        public uint? Confirmations { get; set; }

        public ulong? BlockTime { get; set; }

        public VMState? VMState { get; set; }

        public JObject ToJson(ProtocolSettings protocolSettings)
        {
            JObject json = Utility.TransactionToJson(Transaction, protocolSettings);
            if (Confirmations != null)
            {
                json["blockhash"] = BlockHash.ToString();
                json["confirmations"] = Confirmations;
                json["blocktime"] = BlockTime;
                if (VMState != null)
                {
                    json["vmstate"] = VMState;
                }
            }
            return json;
        }

        public static RpcTransaction FromJson(JObject json, ProtocolSettings protocolSettings)
        {
            RpcTransaction transaction = new RpcTransaction
            {
                Transaction = Utility.TransactionFromJson(json, protocolSettings)
            };
            if (json["confirmations"] != null)
            {
                transaction.BlockHash = UInt256.Parse(json["blockhash"].AsString());
                transaction.Confirmations = (uint)json["confirmations"].AsNumber();
                transaction.BlockTime = (ulong)json["blocktime"].AsNumber();
                transaction.VMState = json["vmstate"]?.TryGetEnum<VMState>();
            }
            return transaction;
        }
    }
}
