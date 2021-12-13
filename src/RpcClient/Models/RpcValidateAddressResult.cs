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
    public class RpcValidateAddressResult
    {
        public string Address { get; set; }

        public bool IsValid { get; set; }

        public JObject ToJson()
        {
            JObject json = new();
            json["address"] = Address;
            json["isvalid"] = IsValid;
            return json;
        }

        public static RpcValidateAddressResult FromJson(JObject json)
        {
            return new RpcValidateAddressResult
            {
                Address = json["address"].AsString(),
                IsValid = json["isvalid"].AsBoolean()
            };
        }
    }
}
