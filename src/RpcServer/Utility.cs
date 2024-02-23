// Copyright (C) 2015-2024 The Neo Project.
//
// Utility.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
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

        public static JObject NativeContractToJson(this NativeContract contract, NeoSystem system)
        {
            var state = contract.GetContractState(system.Settings,
                NativeContract.Ledger.CurrentIndex(system.StoreView));

            return new JObject
            {
                ["id"] = contract.Id,
                ["hash"] = contract.Hash.ToString(),
                ["nef"] = state.Nef.ToJson(),
                ["manifest"] = state.Manifest.ToJson()
            };
        }
    }
}
