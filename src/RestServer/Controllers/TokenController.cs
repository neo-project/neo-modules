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
using Neo.Plugins.RestServer.Models.Token;
using Neo.Plugins.RestServer.Tokens;
using Neo.SmartContract.Native;
using Neo.Plugins.RestServer.Helpers;
using Neo.Plugins.RestServer.Extensions;
using Neo.Plugins.RestServer.Exceptions;
using System.Net.Mime;

namespace Neo.Plugins.RestServer.Controllers
{
    [Route("/api/v1/tokens")]
    [Produces(MediaTypeNames.Application.Json)]
    [ApiController]
    public class TokenController : ControllerBase
    {
        private readonly RestServerSettings _settings;
        private readonly NeoSystem _neosystem;

        public TokenController()
        {
            _neosystem = RestServerPlugin.NeoSystem ?? throw new NodeNetworkException();
            _settings = RestServerSettings.Current;
        }

        #region NEP-17

        [HttpGet("nep-17", Name = "GetNep17Tokens")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GetNEP17(
            [FromQuery(Name = "page")]
            int skip = 1,
            [FromQuery(Name = "size")]
            int take = 1)
        {
            if (skip < 1 || take < 1 || take > _settings.MaxPageSize)
                throw new InvalidParameterRangeException();
            var tokenList = NativeContract.ContractManagement.ListContracts(_neosystem.StoreView);
            var vaildContracts = tokenList
                .Where(w => ContractHelper.IsNep17Supported(w))
                .OrderBy(o => o.Manifest.Name)
                .Skip((skip - 1) * take)
                .Take(take);
            var listResults = new List<NEP17TokenModel>();
            foreach (var contract in vaildContracts)
            {
                if (ContractHelper.IsNep17Supported(contract) == false)
                    continue;
                try
                {
                    var token = new NEP17Token(_neosystem, contract.Hash, _settings);
                    listResults.Add(token.ToModel());
                }
                catch
                {
                }
            }
            if (listResults.Any() == false) return NoContent();
            return Ok(listResults);
        }

        [HttpGet("nep-17/count", Name = "GetNep17TokenCount")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GetNEP17Count()
        {
            return Ok(new { Count = NativeContract.ContractManagement.ListContracts(_neosystem.StoreView).Count(c => ContractHelper.IsNep17Supported(c)) });
        }

        [HttpGet("nep-17/{scripthash:required}/balanceof/{address:required}", Name = "GetNep17TokenBalanceOf")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GetNEP17(
            [FromRoute(Name = "scripthash")]
            UInt160 tokenAddessOrScripthash,
            [FromRoute(Name = "address")]
            UInt160 lookupAddressOrScripthash)
        {
            var contract = NativeContract.ContractManagement.GetContract(_neosystem.StoreView, tokenAddessOrScripthash);
            if (contract == null)
                throw new ContractNotFoundException(tokenAddessOrScripthash);
            if (ContractHelper.IsNep17Supported(contract) == false)
                throw new Nep17NotSupportedException(tokenAddessOrScripthash);
            try
            {
                var token = new NEP17Token(_neosystem, tokenAddessOrScripthash, _settings);
                return Ok(new TokenBalanceModel()
                {
                    Name = token.Name,
                    ScriptHash = token.ScriptHash,
                    Symbol = token.Symbol,
                    Decimals = token.Decimals,
                    Balance = token.BalanceOf(lookupAddressOrScripthash).Value,
                    TotalSupply = token.TotalSupply().Value,
                });
            }
            catch
            {
                throw new Nep17NotSupportedException(tokenAddessOrScripthash);
            }
        }

        #endregion

        #region NEP-11

        [HttpGet("nep-11", Name = "GetNep11Tokens")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GetNEP11(
            [FromQuery(Name = "page")]
            int skip = 1,
            [FromQuery(Name = "size")]
            int take = 1)
        {
            if (skip < 1 || take < 1 || take > _settings.MaxPageSize)
                throw new InvalidParameterRangeException();
            var tokenList = NativeContract.ContractManagement.ListContracts(_neosystem.StoreView);
            var vaildContracts = tokenList
                .Where(w => ContractHelper.IsNep11Supported(w))
            .OrderBy(o => o.Manifest.Name)
                .Skip((skip - 1) * take)
                .Take(take);
            var listResults = new List<NEP11TokenModel>();
            foreach (var contract in vaildContracts)
            {
                if (ContractHelper.IsNep11Supported(contract) == false)
                    continue;
                try
                {
                    var token = new NEP11Token(_neosystem, contract.Hash, _settings);
                    listResults.Add(token.ToModel());
                }
                catch
                {
                }
            }
            if (listResults.Any() == false) return NoContent();
            return Ok(listResults);
        }

        [HttpGet("nep-11/count", Name = "GetNep11TokenCount")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GetNEP11Count()
        {
            return Ok(new { Count = NativeContract.ContractManagement.ListContracts(_neosystem.StoreView).Count(c => ContractHelper.IsNep11Supported(c)) });
        }

        [HttpGet("nep-11/{scripthash:required}/balanceof/{address:required}", Name = "GetNep11TokenBalanceOf")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GetNEP11(
            [FromRoute(Name = "scripthash")]
            UInt160 sAddressHash,
            [FromRoute(Name = "address")]
            UInt160 addressHash)
        {
            var contract = NativeContract.ContractManagement.GetContract(_neosystem.StoreView, sAddressHash);
            if (contract == null)
                throw new ContractNotFoundException(sAddressHash);
            if (ContractHelper.IsNep11Supported(contract) == false)
                throw new Nep11NotSupportedException(sAddressHash);
            try
            {
                var token = new NEP11Token(_neosystem, sAddressHash, _settings);
                return Ok(new TokenBalanceModel()
                {
                    Name = token.Name,
                    ScriptHash = token.ScriptHash,
                    Symbol = token.Symbol,
                    Decimals = token.Decimals,
                    Balance = token.BalanceOf(addressHash).Value,
                    TotalSupply = token.TotalSupply().Value,
                });
            }
            catch
            {
                throw new Nep11NotSupportedException(sAddressHash);
            }
        }

        #endregion

        // Gets every single NEP17/NEP11 on the blockchain balance by address (scriptHash).
        [HttpGet("balanceof/{address:required}", Name = "GetAllTokensBalanceOf")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GetBalances(
            [FromRoute(Name = "address")]
            UInt160 addressOrScripthash)
        {
            var tokenList = NativeContract.ContractManagement.ListContracts(_neosystem.StoreView);
            var validContracts = tokenList
                .Where(w => ContractHelper.IsNep17Supported(w) || ContractHelper.IsNep11Supported(w))
                .OrderBy(o => o.Manifest.Name);
            var listResults = new List<TokenBalanceModel>();
            foreach (var contract in validContracts)
            {
                try
                {
                    var token = new NEP17Token(_neosystem, contract.Hash, _settings);
                    var balance = token.BalanceOf(addressOrScripthash).Value;
                    if (balance == 0) continue;
                    listResults.Add(new()
                    {
                        Name = token.Name,
                        ScriptHash = token.ScriptHash,
                        Symbol = token.Symbol,
                        Decimals = token.Decimals,
                        Balance = balance,
                        TotalSupply = token.TotalSupply().Value,
                    });

                    var nft = new NEP11Token(_neosystem, contract.Hash, _settings);
                    balance = nft.BalanceOf(addressOrScripthash).Value;
                    if (balance == 0) continue;
                    listResults.Add(new()
                    {
                        Name = nft.Name,
                        ScriptHash = nft.ScriptHash,
                        Symbol = nft.Symbol,
                        Balance = balance,
                        Decimals = nft.Decimals,
                        TotalSupply = nft.TotalSupply().Value,
                    });
                }
                catch
                {
                }
            }
            return Ok(listResults);
        }
    }
}
