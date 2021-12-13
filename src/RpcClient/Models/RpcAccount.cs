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

namespace Neo.Network.RPC.Models
{
    public class RpcAccount
    {
        public string Address { get; set; }

        public bool HasKey { get; set; }

        public string Label { get; set; }

        public bool WatchOnly { get; set; }

        public JObject ToJson()
        {
            return new JObject
            {
                ["address"] = Address,
                ["haskey"] = HasKey,
                ["label"] = Label,
                ["watchonly"] = WatchOnly
            };
        }

        public static RpcAccount FromJson(JObject json)
        {
            return new RpcAccount
            {
                Address = json["address"].AsString(),
                HasKey = json["haskey"].AsBoolean(),
                Label = json["label"]?.AsString(),
                WatchOnly = json["watchonly"].AsBoolean(),
            };
        }
    }
}
