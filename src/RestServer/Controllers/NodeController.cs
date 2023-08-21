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
using Neo.Plugins.RestServer.Exceptions;
using Neo.Plugins.RestServer.Extensions;
using Neo.SmartContract.Native;

namespace Neo.Plugins.RestServer.Controllers
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
            _neosystem = RestServerPlugin.NeoSystem ?? throw new NodeNetworkException();
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
                Nodes = rNodes.Select(s => s.ToModel()),
            });
        }

        [HttpGet("peers")]
        public IActionResult GetPeers()
        {
            var rNodes = _neolocalnode
                .GetRemoteNodes()
                .OrderByDescending(o => o.LastBlockIndex)
                .ToArray();

            return Ok(rNodes.Select(s => s.ToModel()));
        }

        [HttpGet("plugins")]
        public IActionResult GetPlugins() =>
            Ok(Plugin.Plugins.Select(s => new
            {
                s.Name,
                Version = s.Version.ToString(3),
                s.Description,
            }));

        [HttpGet("settings")]
        public IActionResult GetSettings() =>
            Ok(_neosystem.Settings.ToModel());
    }
}
