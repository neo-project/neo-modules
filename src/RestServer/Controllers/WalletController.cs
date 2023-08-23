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
using Neo.Network.P2P.Payloads;
using Neo.Plugins.RestServer.Exceptions;
using Neo.Plugins.RestServer.Extensions;
using Neo.Plugins.RestServer.Models.Wallet;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.Wallets;
using Neo.Wallets.NEP6;
using System.Net.Mime;
using System.Numerics;

namespace Neo.Plugins.RestServer.Controllers
{
    [Route("/api/v1/wallet")]
    [Produces(MediaTypeNames.Application.Json)]
    [ApiController]
    public class WalletController : ControllerBase
    {
        internal static WalletSessionManager WalletSessions { get; } = new();

        private readonly NeoSystem _neosystem;
        private readonly RestServerSettings _settings;

        public WalletController()
        {
            _neosystem = RestServerPlugin.NeoSystem ?? throw new NodeNetworkException();
            _settings = RestServerSettings.Current;
        }

        [HttpPost("open", Name = "WalletOpen")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult OpenWallet(
            [FromBody]
            WalletOpenModel model)
        {
            if (string.IsNullOrEmpty(model.Path))
                throw new JsonPropertyNullOrEmptyException(nameof(model.Path));
            if (string.IsNullOrEmpty(model.Password))
                throw new JsonPropertyNullOrEmptyException(nameof(model.Password));
            if (System.IO.File.Exists(model.Path) == false)
                throw new FileNotFoundException(null, model.Path);
            var wallet = Wallet.Open(model.Path, model.Password, _neosystem.Settings);
            if (wallet == null)
                throw new WalletOpenException($"File '{model.Path}' could not be opened.");
            var sessionId = Guid.NewGuid();
            WalletSessions[sessionId] = new WalletSession(wallet);
            return Ok(new
            {
                SessionId = sessionId.ToString("n"),
            });
        }

        [HttpGet("{session:required}/close", Name = "WalletClose")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult CloseWallet(
            [FromRoute(Name = "session")]
            Guid session)
        {
            if (WalletSessions.ContainsKey(session) == false)
                throw new KeyNotFoundException(session.ToString("n"));
            if (WalletSessions.TryRemove(session, out _) == false)
                throw new WalletSessionException("Failed to remove session.");
            return Ok();
        }
        [HttpGet("{session:required}/export", Name = "WalletExportKeys")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult WalletExport(
            [FromRoute(Name = "session")]
            Guid sessionId)
        {
            if (WalletSessions.ContainsKey(sessionId) == false)
                throw new KeyNotFoundException(sessionId.ToString("n"));
            var session = WalletSessions[sessionId];
            session.ResetExpiration();
            var wallet = session.Wallet;
            var keys = wallet.GetAccounts().Where(p => p.HasKey)
                .Select(s => new WalletExportKeyModel()
                {
                    ScriptHash = s.ScriptHash,
                    Address = s.Address,
                    Wif = s.GetKey().Export()
                }).ToArray();
            return Ok(keys);
        }

        [HttpGet("{session:required}/export/{address:required}", Name = "WalletExportKeysByAddressOrScripthash")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult WalletExportKey(
            [FromRoute(Name = "session")]
            Guid sessionId,
            [FromRoute(Name = "address")]
            UInt160 scriptHash)
        {
            if (WalletSessions.ContainsKey(sessionId) == false)
                throw new KeyNotFoundException(sessionId.ToString("n"));
            var session = WalletSessions[sessionId];
            session.ResetExpiration();
            var wallet = session.Wallet;
            var account = wallet.GetAccount(scriptHash);
            var key = account.GetKey();
            return Ok(new WalletExportKeyModel
            {
                ScriptHash = account.ScriptHash,
                Address = account.Address,
                Wif = key.Export(),
            });
        }

        [HttpPost("{session:required}/address/create", Name = "WalletCreateAddress")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult CreateNewAddress(
            [FromRoute(Name = "session")]
            Guid sessionId,
            [FromBody]
            WalletCreateAccountModel model)
        {
            if (WalletSessions.ContainsKey(sessionId) == false)
                throw new KeyNotFoundException(sessionId.ToString("n"));
            var session = WalletSessions[sessionId];
            session.ResetExpiration();
            var wallet = session.Wallet;
            var account = model.PrivateKey == null || model.PrivateKey.Length == 0 ?
                wallet.CreateAccount() :
                wallet.CreateAccount(model.PrivateKey);
            if (wallet is NEP6Wallet nep6)
                nep6.Save();
            return Ok(new
            {
                account.Address,
            });
        }

        [HttpGet("{session:required}/balance/{asset:required}", Name = "WalletBalanceOf")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GetWalletBalance(
            [FromRoute(Name = "session")]
            Guid sessionId,
            [FromRoute(Name = "asset")]
            UInt160 scriptHash)
        {
            if (WalletSessions.ContainsKey(sessionId) == false)
                throw new KeyNotFoundException(sessionId.ToString("n"));
            var session = WalletSessions[sessionId];
            session.ResetExpiration();
            var wallet = session.Wallet;
            var balance = wallet.GetAvailable(_neosystem.StoreView, scriptHash);
            return Ok(new
            {
                balance,
            });
        }

        [HttpGet("{session:required}/UnClaimedGas", Name = "GetUnClaimedGas")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult GetUnClaimedGas(
            [FromRoute(Name = "session")]
            Guid sessionId)
        {
            if (WalletSessions.ContainsKey(sessionId) == false)
                throw new KeyNotFoundException(sessionId.ToString("n"));
            var session = WalletSessions[sessionId];
            session.ResetExpiration();
            var wallet = session.Wallet;
            using var snapshot = _neosystem.GetSnapshot();
            uint height = NativeContract.Ledger.CurrentIndex(snapshot) + 1;
            BigInteger gas = BigInteger.Zero;
            foreach (var account in wallet.GetAccounts().Select(s => s.ScriptHash))
                gas += NativeContract.NEO.UnclaimedGas(snapshot, account, height);
            return Ok(new
            {
                Balance = new BigDecimal(gas, NativeContract.GAS.Decimals),
            });
        }

        [HttpPost("{session:required}/import", Name = "WalletImportByWif")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult ImportPrivateKey(
            [FromRoute(Name = "session")]
            Guid sessionId,
            [FromBody]
            WalletImportKey model)
        {
            if (WalletSessions.ContainsKey(sessionId) == false)
                throw new KeyNotFoundException(sessionId.ToString("n"));
            var session = WalletSessions[sessionId];
            session.ResetExpiration();
            var wallet = session.Wallet;
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

        [HttpGet("{session:required}/ListAddress", Name = "GetWalletListAddress")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult ListAddresses(
            [FromRoute(Name = "session")]
            Guid sessionId)
        {
            if (WalletSessions.ContainsKey(sessionId) == false)
                throw new KeyNotFoundException(sessionId.ToString("n"));
            var session = WalletSessions[sessionId];
            session.ResetExpiration();
            var wallet = session.Wallet;
            var accounts = new List<WalletAddressModel>();
            foreach (var account in wallet.GetAccounts())
                accounts.Add(new WalletAddressModel()
                {
                    Address = account.Address,
                    HasKey = account.HasKey,
                    Label = account.Label,
                    WatchOnly = account.WatchOnly,
                });
            return Ok(accounts);
        }

        [HttpGet("{session:required}/delete/{account:required}", Name = "WalletDeleteAccountByAddressOrScriptHash")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult WalletDeleteAccount(
            [FromRoute(Name = "session")]
            Guid sessionId,
            [FromRoute(Name = "account")]
            UInt160 scriptHash)
        {
            if (WalletSessions.ContainsKey(sessionId) == false)
                throw new KeyNotFoundException(sessionId.ToString("n"));
            var session = WalletSessions[sessionId];
            session.ResetExpiration();
            var wallet = session.Wallet;
            if (wallet.DeleteAccount(scriptHash) == false)
                throw new WalletException($"Could not delete '{scriptHash}' account.");
            else
            {
                if (wallet is NEP6Wallet wallet6)
                    wallet6.Save();
                return Ok();
            }
        }

        [HttpPost("{session:required}/transfer", Name = "WalletTransferAssets")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult WalletTransferAssets(
            [FromRoute(Name = "session")]
            Guid sessionId,
            [FromBody]
            WalletSendModel model)
        {
            if (WalletSessions.ContainsKey(sessionId) == false)
                throw new KeyNotFoundException(sessionId.ToString("n"));
            var session = WalletSessions[sessionId];
            session.ResetExpiration();
            var wallet = session.Wallet;
            using var snapshot = _neosystem.GetSnapshot();
            var descriptor = new AssetDescriptor(snapshot, _neosystem.Settings, model.AssetId);
            var amount = new BigDecimal(model.Amount, descriptor.Decimals);
            if (amount.Sign <= 0)
                throw new WalletException($"Invalid Amount.");
            var signers = model.Signers?.Select(s => new Signer() { Scopes = WitnessScope.CalledByEntry, Account = s }).ToArray();
            var tx = wallet.MakeTransaction(snapshot,
                new[]
                {
                    new TransferOutput()
                    {
                        AssetId = model.AssetId,
                        Value = amount,
                        ScriptHash = model.To,
                        Data = model.Data,
                    },
                },
                model.From, signers);
            if (tx == null)
                throw new WalletInsufficientFundsException();
            var totalFees = new BigDecimal((BigInteger)(tx.SystemFee + tx.NetworkFee), NativeContract.GAS.Decimals);
            if (totalFees.Value > _settings.MaxTransactionFee)
                throw new WalletException("The transaction fees are to much.");
            var context = new ContractParametersContext(snapshot, tx, _neosystem.Settings.Network);
            wallet.Sign(context);
            if (context.Completed == false)
                throw new WalletException("Transaction could not be completed at this time.");
            tx.Witnesses = context.GetWitnesses();
            _neosystem.Blockchain.Tell(tx);
            return Ok(tx);
        }
    }
}
