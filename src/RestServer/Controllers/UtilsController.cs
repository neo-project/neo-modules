// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Neo.Plugins.RestServer.Exceptions;
using Neo.Wallets;
using System.Net.Mime;

namespace Neo.Plugins.RestServer.Controllers
{
    [Route("/api/v1/utils")]
    [Produces(MediaTypeNames.Application.Json)]
    [ApiController]
    public class UtilsController : ControllerBase
    {
        private readonly NeoSystem _neosystem;

        public UtilsController()
        {
            _neosystem = RestServerPlugin.NeoSystem ?? throw new NodeNetworkException();
        }

        #region Validation

        [HttpGet("{hash:required}/address", Name = "GetAddressByScripthash")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult ScriptHashToWalletAddress(
            [FromRoute(Name = "hash")]
            string hash)
        {
            try
            {
                if (UInt160.TryParse(hash, out var scripthash) == false)
                    throw new ScriptHashFormatException();
                return Ok(new { Address = scripthash.ToAddress(_neosystem.Settings.AddressVersion) });
            }
            catch (FormatException)
            {
                throw new ScriptHashFormatException();
            }
        }

        [HttpGet("{address:required}/scripthash", Name = "GetScripthashByAddress")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult WalletAddressToScriptHash(
            [FromRoute(Name = "address")]
            string addr)
        {
            try
            {
                return Ok(new { ScriptHash = addr.ToScriptHash(_neosystem.Settings.AddressVersion) });
            }
            catch (FormatException)
            {
                throw new AddressFormatException();
            }
        }

        [HttpGet("{address:required}/validate", Name = "IsValidAddressOrScriptHash")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult ValidateAddress(
            [FromRoute(Name = "address")]
            string addr)
        {
            UInt160 scriptHash = UInt160.Zero;
            try
            {
                if (UInt160.TryParse(addr, out scriptHash) == false)
                    scriptHash = addr.ToScriptHash(_neosystem.Settings.AddressVersion);

            }
            catch (FormatException) { }
            return Ok(new
            {
                Address = addr,
                IsValid = scriptHash != UInt160.Zero,
            });
        }

        #endregion
    }
}
