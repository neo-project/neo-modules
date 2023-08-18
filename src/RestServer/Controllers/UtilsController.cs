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
using Neo.Plugins.RestServer;
using Neo.Wallets;

namespace Neo.Plugins.Controllers
{
    [Route("/api/v1/utils")]
    [ApiController]
    public class UtilsController : ControllerBase
    {
        private readonly NeoSystem _neosystem;

        public UtilsController()
        {
            _neosystem = RestServerPlugin.NeoSystem;
        }

        [HttpGet("{hash:required}/address")]
        public IActionResult ScriptHashToWalletAddress(
            [FromRoute(Name = "hash")]
            string hash)
        {
            if (UInt160.TryParse(hash, out var scripthash) == false) return BadRequest(nameof(hash));
            return Ok(new { WalletAddress = scripthash.ToAddress(_neosystem.Settings.AddressVersion) });
        }

        [HttpGet("{address:required}/scripthash")]
        public IActionResult WalletAddressToScriptHash(
            [FromRoute(Name = "address")]
            string addr)
        {
            try
            {
                return Ok(new { ScriptHash = addr.ToScriptHash(_neosystem.Settings.AddressVersion) });
            }
            catch
            {
                return BadRequest(nameof(addr));
            }
        }
    }
}
