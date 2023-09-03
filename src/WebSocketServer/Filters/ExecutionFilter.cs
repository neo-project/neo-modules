// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Network.RPC is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
namespace Neo.Plugins.WebSocketServer.Filters;

public class ExecutionFilter : Filter
{
    public string? State { get; set; }
    public UInt256? Container { get; set; }

    public override Filter FromJson(JObject json)
    {
        State = json["state"]?.GetString();
        Container = UInt256.Parse(json["container"]?.GetString());
        return this;
    }
}
