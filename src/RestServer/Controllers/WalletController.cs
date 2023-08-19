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
using Neo.Plugins.RestServer.Exceptions;
using Neo.Plugins.RestServer.Extensions;
using Neo.Plugins.RestServer.Models.Wallet;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.Wallets;
using Neo.Wallets.NEP6;
using System.Collections.Concurrent;
using System.Net.Mime;
using System.Numerics;

namespace Neo.Plugins.RestServer.Controllers
{
    [Route("/api/v1/wallet")]
    [ApiController]
    public class WalletController : ControllerBase
    {
        private static readonly ConcurrentDictionary<Guid, Wallet> _walletSessions = new();

        private readonly NeoSystem _neosystem;
        private readonly RestServerSettings _settings;

        public WalletController(
            RestServerSettings settings)
        {
            _neosystem = RestServerPlugin.NeoSystem ?? throw new NodeNetworkException();
            _settings = settings;
        }

        [HttpPost("open")]
        public IActionResult OpenWallet(
            [FromBody]
            WalletCredentialsModel model)
        {
            if (ModelState.IsValid == false) return BadRequest();
            if (System.IO.File.Exists(model.Path) == false) return NotFound();
            var wallet = Wallet.Open(model.Path, model.Password, _neosystem.Settings);
            if (wallet == null) return Forbid();
            var sessionId = Guid.NewGuid();
            _walletSessions[sessionId] = wallet;
            return Ok(new
            {
                SessionId = sessionId.ToString("n"),
            });
        }

        [HttpGet("{session:required}/close")]
        public IActionResult CloseWallet(
            [FromRoute(Name = "session")]
            string session)
        {
            if (Guid.TryParse(session, out Guid sessionId) == false) return BadRequest();
            if (_walletSessions.ContainsKey(sessionId) == false) return NotFound();
            if (_walletSessions.TryRemove(sessionId, out _) == false) return StatusCode(StatusCodes.Status424FailedDependency);
            return Ok();
        }

        [HttpGet("{session:required}/DumpPrivKey/{address}")]
        public IActionResult DumpPrivKey(
            [FromRoute(Name = "session")]
            string session,
            [FromRoute(Name = "address")]
            string address)
        {
            if (Guid.TryParse(session, out Guid sessionId) == false) return BadRequest();
            if (_walletSessions.ContainsKey(sessionId) == false) return NotFound();
            if (UInt160.TryParse(address, out var scriptHash) == false)
                scriptHash = address.ToScriptHash(_neosystem.Settings.AddressVersion);
            var account = _walletSessions[sessionId].GetAccount(scriptHash);
            var key = account.GetKey();
            var pubKey = key.PublicKey.EncodePoint(true).ToHexString();
            return Ok(new[]
            {
                new WalletExportKeyModel
                {
                    Public = pubKey,
                    Private = key.Export(),
                    Format = WalletKeyFormat.WIF,
                },
                new WalletExportKeyModel
                {
                    Public = pubKey,
                    Private = Convert.ToHexString(key.PrivateKey),
                    Format = WalletKeyFormat.HEX,
                }
            });
        }

        [HttpPost("{session:required}/address/create")]
        public IActionResult CreateNewAddress(
            [FromRoute(Name = "session")]
            string session,
            [FromBody]
            WalletCreateAccountModel model)
        {
            if (Guid.TryParse(session, out Guid sessionId) == false) return BadRequest();
            if (_walletSessions.ContainsKey(sessionId) == false) return NotFound();
            if (ModelState.IsValid == false) return BadRequest();
            var wallet = _walletSessions[sessionId];
            var account = model.PrivateKey == null || model.PrivateKey.Length == 0 ?
                wallet.CreateAccount() :
                wallet.CreateAccount(model.PrivateKey);
            if (wallet is NEP6Wallet nep6)
                nep6.Save();
            return Ok(new
            {
                AccountAddress = account.Address,
            });
        }

        [HttpGet("{session:required}/balance/{asset:required}")]
        public IActionResult GetWalletBalance(
            [FromRoute(Name = "session")]
            string session,
            [FromRoute(Name = "asset")]
            string asset)
        {
            if (Guid.TryParse(session, out Guid sessionId) == false) return BadRequest();
            if (_walletSessions.ContainsKey(sessionId) == false) return NotFound();
            if (UInt160.TryParse(asset, out var scriptHash) == false)
                scriptHash = asset.ToScriptHash(_neosystem.Settings.AddressVersion);
            var balance = _walletSessions[sessionId].GetAvailable(_neosystem.StoreView, scriptHash);
            return Ok(new
            {
                Balance = balance.ToString(),
            });
        }

        [HttpGet("{session:required}/UnClaimedGas")]
        public IActionResult GetUnClaimedGas(
            [FromRoute(Name = "session")]
            string session)
        {
            if (Guid.TryParse(session, out Guid sessionId) == false) return BadRequest();
            if (_walletSessions.ContainsKey(sessionId) == false) return NotFound();
            var wallet = _walletSessions[sessionId];
            using var snapshot = _neosystem.GetSnapshot();
            uint height = NativeContract.Ledger.CurrentIndex(snapshot) + 1;
            BigInteger gas = BigInteger.Zero;
            foreach (var account in wallet.GetAccounts().Select(s => s.ScriptHash))
                gas += NativeContract.NEO.UnclaimedGas(snapshot, account, height);
            return Ok(new
            {
                Balance = new BigDecimal(gas, NativeContract.GAS.Decimals).ToString(),
                BalanceValue = gas,
            });
        }

        [HttpPost("{session:required}/import")]
        public IActionResult ImportPrivateKey(
            [FromRoute(Name = "session")]
            string session,
            WalletImportKey model)
        {
            if (Guid.TryParse(session, out Guid sessionId) == false) return BadRequest();
            if (_walletSessions.ContainsKey(sessionId) == false) return NotFound();
            if (ModelState.IsValid == false) return BadRequest();
            var wallet = _walletSessions[sessionId];
            var account = wallet.Import(model.Wif);
            if (wallet is NEP6Wallet nep6)
                nep6.Save();
            return Ok(new WalletAddressModel()
            {
                Address = account.Address,
                HasKey = account.HasKey,
                Label = account.Label,
                WatchOnly = account.WatchOnly,
            });
        }

        [HttpGet("{session:required}/ListAddress")]
        public IActionResult ListAddresses(
            [FromRoute(Name = "session")]
            string session)
        {
            if (Guid.TryParse(session, out Guid sessionId) == false) return BadRequest();
            if (_walletSessions.ContainsKey(sessionId) == false) return NotFound();
            var wallet = _walletSessions[sessionId];
            var accounts = new List<WalletAddressModel>();
            foreach (var account in wallet.GetAccounts())
                accounts.Add(new WalletAddressModel()
                {
                    Address = account.Address,
                    HasKey = account.HasKey,
                    Label = account.Label,
                    WatchOnly = account.WatchOnly,
                });
            return Ok(accounts.ToArray());
        }

        [HttpPost("{session:required}/transfer")]
        public IActionResult WalletTransferAssets(
            [FromRoute(Name = "session")]
            string session,
            [FromBody]
            WalletSendModel model)
        {
            if (Guid.TryParse(session, out Guid sessionId) == false) return BadRequest();
            if (_walletSessions.ContainsKey(sessionId) == false) return NotFound();
            if (ModelState.IsValid == false) return BadRequest();
            var wallet = _walletSessions[sessionId];
            using var snapshot = _neosystem.GetSnapshot();
            var descriptor = new AssetDescriptor(snapshot, _neosystem.Settings, model.AssetId);
            var amount = new BigDecimal(model.Amount, descriptor.Decimals);
            if (amount.Sign <= 0) return BadRequest(nameof(model.Amount));
            var tx = wallet.MakeTransaction(snapshot, new[]
            {
                new TransferOutput()
                {
                    AssetId = model.AssetId,
                    Value = amount,
                    ScriptHash = model.To,
                }
            });
            if (tx == null) return BadRequest("Tranasction");
            var transContext = new ContractParametersContext(snapshot, tx, _neosystem.Settings.Network);
            wallet.Sign(transContext);
            if (transContext.Completed == false)
                return Content(transContext.ToJson().ToString(), MediaTypeNames.Application.Json);
            tx.Witnesses = transContext.GetWitnesses();
            // What if this is completed? Process and sign again?
            if (tx.Size > 1024)
            {
                var calFee = tx.Size * NativeContract.Policy.GetFeePerByte(snapshot) + unchecked((long)NativeContract.GAS.Factor);
                if (tx.NetworkFee < calFee) tx.NetworkFee = calFee;

            }
            if (tx.NetworkFee > _settings.MaxTransactionFee) return BadRequest("The necessary fee is more than the MaxTransactionFee, this transaction has failed. Please increase your MaxTransactionFee value.");

            // Why do this again? What if its completed already? Copied from RPC Server...
            transContext = new ContractParametersContext(snapshot, tx, _neosystem.Settings.Network);
            wallet.Sign(transContext);
            if (transContext.Completed == false)
                return Content(transContext.ToJson().ToString(), MediaTypeNames.Application.Json);
            else
            {
                tx.Witnesses = transContext.GetWitnesses();
                _neosystem.Blockchain.Tell(tx);
                return Ok(tx.ToModel());
            }
        }
    }
}
