// Copyright (C) 2015-2024 The Neo Project.
//
// WebSocketRequestMessage.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using System;

namespace Neo.Plugins
{
    internal class WebSocketRequestMessage
    {
        public Version? Version { get; set; }
        public int RequestId { get; set; }
        public string? Method { get; set; }
        public JArray? Params { get; set; }

        public static WebSocketRequestMessage FromJson(JToken? message) =>
            new()
            {
                Version = message?["version"] == null ? null : new(message["version"]!.AsString()),
                RequestId = message?["requestid"] == null ? 0 : checked((int)message["requestid"]!.AsNumber()),
                Method = message?["method"]?.AsString(),
                Params = message?["params"] == null ? null : (JArray)message["params"]!,
            };

    }
}
