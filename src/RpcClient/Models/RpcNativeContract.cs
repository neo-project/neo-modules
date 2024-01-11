// Copyright (C) 2015-2024 The Neo Project.
//
// RpcNativeContract.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;

namespace Neo.Network.RPC.Models
{
    public class RpcNativeContract
    {
        public int Id { get; set; }
        public UInt160 Hash { get; set; }
        public NefFile Nef { get; set; }
        public ContractManifest Manifest { get; set; }

        public static RpcNativeContract FromJson(JObject json)
        {
            return new RpcNativeContract
            {
                Id = (int)json["id"].AsNumber(),
                Hash = UInt160.Parse(json["hash"].AsString()),
                Nef = RpcNefFile.FromJson((JObject)json["nef"]),
                Manifest = ContractManifest.FromJson((JObject)json["manifest"])
            };
        }

        public JObject ToJson()
        {
            return new JObject
            {
                ["id"] = Id,
                ["hash"] = Hash.ToString(),
                ["nef"] = Nef.ToJson(),
                ["manifest"] = Manifest.ToJson()
            };
        }
    }
}
