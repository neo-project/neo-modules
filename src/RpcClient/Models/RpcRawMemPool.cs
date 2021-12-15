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
using System.Collections.Generic;
using System.Linq;

namespace Neo.Network.RPC.Models
{
    public class RpcRawMemPool
    {
        public uint Height { get; set; }

        public List<UInt256> Verified { get; set; }

        public List<UInt256> UnVerified { get; set; }

        public JObject ToJson()
        {
            JObject json = new();
            json["height"] = Height;
            json["verified"] = new JArray(Verified.Select(p => (JObject)p.ToString()));
            json["unverified"] = new JArray(UnVerified.Select(p => (JObject)p.ToString()));
            return json;
        }

        public static RpcRawMemPool FromJson(JObject json)
        {
            return new RpcRawMemPool
            {
                Height = uint.Parse(json["height"].AsString()),
                Verified = ((JArray)json["verified"]).Select(p => UInt256.Parse(p.AsString())).ToList(),
                UnVerified = ((JArray)json["unverified"]).Select(p => UInt256.Parse(p.AsString())).ToList()
            };
        }
    }
}
