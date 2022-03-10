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
using System.Linq;

namespace Neo.Network.RPC.Models
{
    public class RpcPlugin
    {
        public string Name { get; set; }

        public string Version { get; set; }

        public string[] Interfaces { get; set; }

        public JObject ToJson()
        {
            JObject json = new();
            json["name"] = Name;
            json["version"] = Version;
            json["interfaces"] = new JArray(Interfaces.Select(p => (JObject)p));
            return json;
        }

        public static RpcPlugin FromJson(JObject json)
        {
            return new RpcPlugin
            {
                Name = json["name"].AsString(),
                Version = json["version"].AsString(),
                Interfaces = ((JArray)json["interfaces"]).Select(p => p.AsString()).ToArray()
            };
        }
    }
}
