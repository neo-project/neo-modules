// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Akka.Actor;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Neo.IO;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Plugins.RestServer.Exceptions;
using Neo.Plugins.RestServer.Extensions;
using Neo.SmartContract.Native;
using System.Net.NetworkInformation;

namespace Neo.Plugins.RestServer.Controllers
{
    [Route("/api/v1/blackchain")]
    [ApiController]
    public class BlockchainController : ControllerBase
    {
        private readonly NeoSystem _neosystem;
        private readonly RestServerSettings _settings;

        public BlockchainController(
            RestServerSettings settings)
        {
            _neosystem = RestServerPlugin.NeoSystem ?? throw new NodeNetworkException();
            _settings = settings;
        }

        [HttpPost("send/transaction")]
        public IActionResult SendRawTransaction(
            [FromBody]
            string base64String)
        {
            if (string.IsNullOrEmpty(base64String)) return BadRequest();
            var transactionBytes = Convert.FromBase64String(base64String);
            if (transactionBytes?.Length > _settings.MaxTransactionSize) return StatusCode(StatusCodes.Status413PayloadTooLarge);
            var tx = transactionBytes.AsSerializable<Transaction>();
            var reason = _neosystem.Blockchain.Ask<Blockchain.RelayResult>(tx).Result;
            if (reason.Result == VerifyResult.Succeed)
                return Ok(tx.ToModel());
            return UnprocessableEntity(new
            {
                Transaction = tx.ToModel(),
                Reason = reason.Result.ToString(),
            });
        }

        [HttpGet("accounts/gas")]
        public IActionResult ShowGasAccounts()
        {
            var accounts = NativeContract.GAS.ListAccounts(_neosystem.StoreView, _neosystem.Settings);
            return Ok(accounts);
        }

        [HttpGet("accounts/neo")]
        public IActionResult ShowNeoAccounts()
        {
            var accounts = NativeContract.NEO.ListAccounts(_neosystem.StoreView, _neosystem.Settings);
            return Ok(accounts);
        }
    }
}
