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
using System.Numerics;

namespace Neo.Network.RPC.Models
{
    public class RpcValidator
    {
        public string PublicKey { get; set; }

        public BigInteger Votes { get; set; }

        public bool Active { get; set; }

        public JObject ToJson()
        {
            JObject json = new();
            json["publickey"] = PublicKey;
            json["votes"] = Votes.ToString();
            json["active"] = Active;
            return json;
        }

        public static RpcValidator FromJson(JObject json)
        {
            return new RpcValidator
            {
                PublicKey = json["publickey"].AsString(),
                Votes = BigInteger.Parse(json["votes"].AsString()),
                Active = json["active"].AsBoolean()
            };
        }
    }
}
