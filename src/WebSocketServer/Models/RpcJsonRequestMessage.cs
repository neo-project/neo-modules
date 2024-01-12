// Copyright (C) 2015-2024 The Neo Project.
//
// RpcJsonRequestMessage.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using System;

namespace Neo.Plugins.WsRpcJsonServer.Models
{
    internal class RpcJsonRequestMessage
    {
        public Version? Version { get; set; }
        public int Id { get; set; }
        public string? Method { get; set; }
        public JArray? Params { get; set; }

        public static RpcJsonRequestMessage FromJson(JToken? message) =>
            new()
            {
                Version = message?["jsonrpc"] == null ? null : new(message["jsonrpc"]!.AsString()),
                Id = message?["id"] == null ? 0 : unchecked((int)message["id"]!.AsNumber()),
                Method = message?["method"]?.AsString(),
                Params = message?["params"] == null ? null : message["params"]! as JArray,
            };

    }
}
