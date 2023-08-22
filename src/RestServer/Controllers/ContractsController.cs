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
using Neo.Plugins.RestServer.Extensions;
using Neo.Plugins.RestServer.Helpers;
using Neo.SmartContract.Native;
using Newtonsoft.Json.Linq;
using System.Net.Mime;

namespace Neo.Plugins.RestServer.Controllers
{
    [Route("/api/v1/contracts")]
    [Produces(MediaTypeNames.Application.Json)]
    [ApiController]
    public class ContractsController : ControllerBase
    {
        private readonly NeoSystem _neosystem;
        private readonly RestServerSettings _settings;

        public ContractsController()
        {
            _neosystem = RestServerPlugin.NeoSystem ?? throw new NodeNetworkException();
            _settings = RestServerSettings.Current;
        }

        [HttpGet(Name = "GetContracts")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult Get(
            [FromQuery(Name = "page")]
            int skip = 1,
            [FromQuery(Name = "size")]
            int take = 1)
        {
            if (skip < 1 || take < 1 || take > _settings.MaxPageSize)
                throw new InvalidParameterRangeException();
            var contracts = NativeContract.ContractManagement.ListContracts(_neosystem.StoreView);
            if (contracts.Any() == false) return NoContent();
            var contractRequestList = contracts.OrderBy(o => o.Manifest.Name).Skip((skip - 1) * take).Take(take);
            if (contractRequestList.Any() == false) return NoContent();
            return Ok(contractRequestList);
        }

        [HttpGet("count", Name = "GetContractCount")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GetCount()
        {
            var contracts = NativeContract.ContractManagement.ListContracts(_neosystem.StoreView);
            return Ok(new { Count = contracts.Count() });
        }

        [HttpGet("{hash:required}/storage", Name = "GetContractStorage")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GetContractStorage(
            [FromRoute(Name = "hash")]
            UInt160 scripthash)
        {
            if (NativeContract.IsNative(scripthash)) return NoContent();
            var contract = NativeContract.ContractManagement.GetContract(_neosystem.StoreView, scripthash);
            if (contract == null)
                throw new ContractNotFoundException(scripthash);
            var contractStorage = contract.GetStorage(_neosystem.StoreView);
            if (contractStorage.Any() == false) return NoContent();
            return Ok(contractStorage.Select(s => new KeyValuePair<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>(s.key.Key, s.value.Value)));
        }

        [HttpGet("{hash:required}", Name = "GetContract")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GetByScriptHash(
            [FromRoute(Name = "hash")]
            UInt160 scripthash)
        {
            var contracts = NativeContract.ContractManagement.GetContract(_neosystem.StoreView, scripthash);
            if (contracts == null)
                throw new ContractNotFoundException(scripthash);
            return Ok(contracts);
        }

        [HttpGet("{hash:required}/abi", Name = "GetContractAbi")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GetContractAbi(
            [FromRoute(Name = "hash")]
            UInt160 scripthash)
        {
            var contracts = NativeContract.ContractManagement.GetContract(_neosystem.StoreView, scripthash);
            if (contracts == null)
                throw new ContractNotFoundException(scripthash);
            return Ok(contracts.Manifest.Abi);
        }

        [HttpGet("{hash:required}/manifest", Name = "GetContractManifest")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GetContractManifest(
            [FromRoute(Name = "hash")]
            UInt160 scripthash)
        {
            var contracts = NativeContract.ContractManagement.GetContract(_neosystem.StoreView, scripthash);
            if (contracts == null)
                throw new ContractNotFoundException(scripthash);
            return Ok(contracts.Manifest);
        }

        [HttpGet("{hash:required}/nef", Name = "GetContractNefFile")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GetContractNef(
            [FromRoute(Name = "hash")]
            UInt160 scripthash)
        {
            var contracts = NativeContract.ContractManagement.GetContract(_neosystem.StoreView, scripthash);
            if (contracts == null)
                throw new ContractNotFoundException(scripthash);
            return Ok(contracts.Nef);
        }

        [HttpPost("{hash:required}/invoke", Name = "InvokeContractMethod")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult InvokeContract(
            [FromRoute(Name = "hash")]
            UInt160 scripthash,
            [FromQuery(Name = "method")]
            string method,
            [FromBody]
            JToken jparams)
        {
            var contracts = NativeContract.ContractManagement.GetContract(_neosystem.StoreView, scripthash);
            if (contracts == null)
                throw new ContractNotFoundException(scripthash);
            if (string.IsNullOrEmpty(method))
                throw new QueryParameterNotFoundException(nameof(method));
            try
            {
                var engine = ScriptHelper.InvokeMethod(_neosystem.Settings, _settings, _neosystem.StoreView, contracts.Hash, method, jparams);
                return Ok(engine.ToModel());
            }
            catch (Exception ex)
            {
                throw ex?.InnerException ?? ex;
            }
        }
    }
}
