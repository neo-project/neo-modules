// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.AspNetCore.Mvc;
using Neo.IO;
using Neo.Network.P2P;
using Neo.Plugins.RestServer;
using Neo.Plugins.RestServer.Models;
using Neo.SmartContract.Native;

namespace Neo.Plugins.Controllers
{
    [Route("/api/v1/node")]
    [ApiController]
    public class NodeController : ControllerBase
    {
        private readonly LocalNode _neolocalnode;
        private readonly NeoSystem _neosystem;

        public NodeController()
        {
            _neolocalnode = RestServerPlugin.LocalNode;
            _neosystem = RestServerPlugin.NeoSystem;
        }

        [HttpGet]
        public IActionResult Get()
        {
            var rNodes = _neolocalnode
                .GetRemoteNodes()
                .OrderByDescending(o => o.LastBlockIndex)
                .ToArray();

            uint height = NativeContract.Ledger.CurrentIndex(_neosystem.StoreView);
            uint headerHeight = _neosystem.HeaderCache.Last?.Index ?? height;
            int connectedCount = _neolocalnode.ConnectedCount;
            int unconnectedCount = _neolocalnode.UnconnectedCount;

            return Ok(new
            {
                height,
                headerHeight,
                connectedCount,
                unconnectedCount,
                Nodes = rNodes.Select(s =>
                    new RemoteNodeModel()
                    {
                        RemoteAddress = s.Remote.Address.ToString(),
                        RemotePort = s.Remote.Port,
                        ListenTcpPort = s.ListenerTcpPort,
                        LastBlockIndex = s.LastBlockIndex,
                    }),
            });
        }
    }
}
