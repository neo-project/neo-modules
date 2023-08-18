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
using Neo.Plugins.Helpers;
using Neo.Plugins.RestServer;
using Neo.Plugins.RestServer.Extensions;
using Neo.SmartContract.Native;
using Newtonsoft.Json.Linq;

namespace Neo.Plugins.Controllers
{
    [Route("/api/v1/contracts")]
    [ApiController]
    public class ContractsController : ControllerBase
    {
        private readonly uint _maxPageSize;
        private readonly NeoSystem _neosystem;

        public ContractsController(
            RestServerSettings restsettings)
        {
            _neosystem = RestServerPlugin.NeoSystem;
            _maxPageSize = restsettings.MaxPageSize;
        }

        [HttpGet]
        public IActionResult Get(
            [FromQuery(Name = "page")]
            int skip = 1,
            [FromQuery(Name = "size")]
            int take = 1)
        {
            if (skip < 1 || take < 1 || take > _maxPageSize) return StatusCode(StatusCodes.Status416RequestedRangeNotSatisfiable);
            var contracts = NativeContract.ContractManagement.ListContracts(_neosystem.StoreView);
            if (contracts.Any() == false) return NoContent();
            var contractRequestList = contracts.OrderBy(o => o.Manifest.Name).Skip((skip - 1) * take).Take(take);
            if (contractRequestList.Any() == false) return NoContent();
            return Ok(contractRequestList.Select(s => s.ToModel()));
        }

        [HttpGet("count")]
        public IActionResult GetCount()
        {
            var contracts = NativeContract.ContractManagement.ListContracts(_neosystem.StoreView);
            return Ok(new { Count = contracts.Count() });
        }

        [HttpGet("{hash:required}/storage")]
        public IActionResult GetContractStorage(
            [FromRoute(Name = "hash")]
            string hash)
        {
            if (UInt160.TryParse(hash, out var scripthash) == false) return BadRequest(nameof(hash));
            if (NativeContract.IsNative(scripthash)) return NoContent();
            var contract = NativeContract.ContractManagement.GetContract(_neosystem.StoreView, scripthash);
            if (contract == null) return NotFound(nameof(hash));
            var contractStorage = contract.GetStorage(_neosystem.StoreView);
            if (contractStorage.Any() == false) return NoContent();
            return Ok(contractStorage.Select(s => new KeyValuePair<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>(s.key.Key, s.value.Value)));
        }

        [HttpGet("{hash:required}")]
        public IActionResult GetByScriptHash(
            [FromRoute(Name = "hash")]
            string hash)
        {
            if (UInt160.TryParse(hash, out var scripthash) == false) return BadRequest(nameof(hash));
            var contracts = NativeContract.ContractManagement.GetContract(_neosystem.StoreView, scripthash);
            if (contracts == null) return NotFound(nameof(hash));
            return Ok(contracts.ToModel());
        }

        [HttpGet("{hash:required}/abi")]
        public IActionResult GetContractAbi(
            [FromRoute(Name = "hash")]
            string hash)
        {
            if (UInt160.TryParse(hash, out var scripthash) == false) return BadRequest(nameof(hash));
            var contracts = NativeContract.ContractManagement.GetContract(_neosystem.StoreView, scripthash);
            if (contracts == null) return NotFound(nameof(hash));
            return Ok(contracts.Manifest.Abi.ToModel());
        }

        [HttpGet("{hash:required}/manifest")]
        public IActionResult GetContractManifest(
            [FromRoute(Name = "hash")]
            string hash)
        {
            if (UInt160.TryParse(hash, out var scripthash) == false) return BadRequest(nameof(hash));
            var contracts = NativeContract.ContractManagement.GetContract(_neosystem.StoreView, scripthash);
            if (contracts == null) return NotFound(nameof(hash));
            return Ok(contracts.Manifest.ToModel());
        }

        [HttpGet("{hash:required}/nef")]
        public IActionResult GetContractNef(
            [FromRoute(Name = "hash")]
            string hash)
        {
            if (UInt160.TryParse(hash, out var scripthash) == false) return BadRequest(nameof(hash));
            var contracts = NativeContract.ContractManagement.GetContract(_neosystem.StoreView, scripthash);
            if (contracts == null) return NotFound(nameof(hash));
            return Ok(contracts.Nef.ToModel());
        }

        [HttpPost("{hash:required}/invoke")]
        public IActionResult InvokeContract(
            [FromRoute(Name = "hash")]
            string hash,
            [FromQuery(Name = "method")]
            string method,
            [FromBody]
            JToken jparams)
        {
            try
            {
                if (UInt160.TryParse(hash, out var scripthash) == false) return BadRequest(nameof(hash));
                var contracts = NativeContract.ContractManagement.GetContract(_neosystem.StoreView, scripthash);
                if (contracts == null) return NotFound(nameof(hash));
                if (string.IsNullOrEmpty(method)) return BadRequest(nameof(method));
                var engine = ScriptHelper.InvokeMethod(_neosystem.Settings, _neosystem.StoreView, contracts.Hash, method, jparams);
                if (engine == null) return BadRequest();
                return Ok(engine.ToModel());
            }
            catch
            {
                return Conflict();
            }
        }
    }
}
