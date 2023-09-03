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

public class NotificationFilter : Filter
{
    public UInt160? Contract { get; set; }
    public string? Name { get; set; }

    public override Filter FromJson(JObject json)
    {
        Contract = UInt160.Parse(json["contract"]?.GetString());
        Name = json["name"]?.GetString();
        return this;
    }
}
