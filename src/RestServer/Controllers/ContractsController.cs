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
using Neo.Plugins.RestServer.Models;
using Neo.Plugins.RestServer.Models.Error;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Newtonsoft.Json.Linq;
using System.Net.Mime;

namespace Neo.Plugins.RestServer.Controllers
{
    [Route("/api/v1/contracts")]
    [Produces(MediaTypeNames.Application.Json)]
    [Consumes(MediaTypeNames.Application.Json)]
    [ApiExplorerSettings(GroupName = "v1")]
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

        /// <summary>
        /// Get all the smart contracts from the blockchain.
        /// </summary>
        /// <param name="skip" example="1">Page</param>
        /// <param name="take" example="50">Page Size</param>
        /// <returns>An array of Contract object.</returns>
        /// <response code="204">No more pages.</response>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpGet(Name = "GetContracts")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ContractState[]))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
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

        /// <summary>
        /// Gets count of total smart contracts on blockchain.
        /// </summary>
        /// <returns>Count Object</returns>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpGet("count", Name = "GetContractCount")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CountModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
        public IActionResult GetCount()
        {
            var contracts = NativeContract.ContractManagement.ListContracts(_neosystem.StoreView);
            return Ok(new CountModel() { Count = contracts.Count() });
        }

        /// <summary>
        /// Get a smart contract's storage.
        /// </summary>
        /// <param name="scripthash" example="0xed7cc6f5f2dd842d384f254bc0c2d58fb69a4761">ScriptHash</param>
        /// <returns>An array of the Key (Base64) Value (Base64) Pairs objects.</returns>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpGet("{hash:required}/storage", Name = "GetContractStorage")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(KeyValuePair<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>[]))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
        public IActionResult GetContractStorage(
            [FromRoute(Name = "hash")]
            UInt160 scripthash)
        {
            if (NativeContract.IsNative(scripthash)) return NoContent();
            var contract = NativeContract.ContractManagement.GetContract(_neosystem.StoreView, scripthash);
            if (contract == null)
                throw new ContractNotFoundException(scripthash);
            var contractStorage = contract.GetStorage(_neosystem.StoreView);
            return Ok(contractStorage.Select(s => new KeyValuePair<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>>(s.key.Key, s.value.Value)));
        }

        /// <summary>
        /// Get a smart contract.
        /// </summary>
        /// <param name="scripthash" example="0xed7cc6f5f2dd842d384f254bc0c2d58fb69a4761">ScriptHash</param>
        /// <returns>Contract Object.</returns>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpGet("{hash:required}", Name = "GetContract")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ContractState))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
        public IActionResult GetByScriptHash(
            [FromRoute(Name = "hash")]
            UInt160 scripthash)
        {
            var contracts = NativeContract.ContractManagement.GetContract(_neosystem.StoreView, scripthash);
            if (contracts == null)
                throw new ContractNotFoundException(scripthash);
            return Ok(contracts);
        }

        /// <summary>
        /// Get abi of a smart contract.
        /// </summary>
        /// <param name="scripthash" example="0xed7cc6f5f2dd842d384f254bc0c2d58fb69a4761">ScriptHash</param>
        /// <returns>Contract Abi Object.</returns>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpGet("{hash:required}/abi", Name = "GetContractAbi")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ContractAbi))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
        public IActionResult GetContractAbi(
            [FromRoute(Name = "hash")]
            UInt160 scripthash)
        {
            var contracts = NativeContract.ContractManagement.GetContract(_neosystem.StoreView, scripthash);
            if (contracts == null)
                throw new ContractNotFoundException(scripthash);
            return Ok(contracts.Manifest.Abi);
        }

        /// <summary>
        /// Get manifest of a smart contract.
        /// </summary>
        /// <param name="scripthash" example="0xed7cc6f5f2dd842d384f254bc0c2d58fb69a4761">ScriptHash</param>
        /// <returns>Contract Manifest object.</returns>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpGet("{hash:required}/manifest", Name = "GetContractManifest")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ContractManifest))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
        public IActionResult GetContractManifest(
            [FromRoute(Name = "hash")]
            UInt160 scripthash)
        {
            var contracts = NativeContract.ContractManagement.GetContract(_neosystem.StoreView, scripthash);
            if (contracts == null)
                throw new ContractNotFoundException(scripthash);
            return Ok(contracts.Manifest);
        }

        /// <summary>
        /// Get nef of a smart contract.
        /// </summary>
        /// <param name="scripthash" example="0xed7cc6f5f2dd842d384f254bc0c2d58fb69a4761">ScriptHash</param>
        /// <returns>Contract Nef object.</returns>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpGet("{hash:required}/nef", Name = "GetContractNefFile")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(NefFile))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
        public IActionResult GetContractNef(
            [FromRoute(Name = "hash")]
            UInt160 scripthash)
        {
            var contracts = NativeContract.ContractManagement.GetContract(_neosystem.StoreView, scripthash);
            if (contracts == null)
                throw new ContractNotFoundException(scripthash);
            return Ok(contracts.Nef);
        }

        /// <summary>
        /// Invoke a method as ReadOnly Flag on a smart contract.
        /// </summary>
        /// <param name="scripthash" example="0xed7cc6f5f2dd842d384f254bc0c2d58fb69a4761">ScriptHash</param>
        /// <param name="method" example="balanceOf">method name</param>
        /// <param name="aparams">JArray of the contract parameters.</param>
        /// <returns>Execution Engine object.</returns>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpPost("{hash:required}/invoke", Name = "InvokeContractMethod")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ExecutionEngineModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
        public IActionResult InvokeContract(
            [FromRoute(Name = "hash")]
            UInt160 scripthash,
            [FromQuery(Name = "method")]
            string method,
            [FromBody]
            JArray aparams)
        {
            var contracts = NativeContract.ContractManagement.GetContract(_neosystem.StoreView, scripthash);
            if (contracts == null)
                throw new ContractNotFoundException(scripthash);
            if (string.IsNullOrEmpty(method))
                throw new QueryParameterNotFoundException(nameof(method));
            try
            {
                var engine = ScriptHelper.InvokeMethod(_neosystem.Settings, _settings, _neosystem.StoreView, contracts.Hash, method, aparams, out var script);
                return Ok(engine.ToModel());
            }
            catch (Exception ex)
            {
                throw ex?.InnerException ?? ex;
            }
        }
    }
}
