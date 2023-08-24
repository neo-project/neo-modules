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
using Neo.Plugins.RestServer.Helpers;
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
        public IActionResult WalletOpen(
            [FromBody]
            WalletOpenModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Path))
                throw new JsonPropertyNullOrEmptyException(nameof(model.Path));
            if (string.IsNullOrEmpty(model.Password))
                throw new JsonPropertyNullOrEmptyException(nameof(model.Password));
            if (new FileInfo(model.Path).DirectoryName.StartsWith(Environment.CurrentDirectory, StringComparison.InvariantCultureIgnoreCase) == false)
                throw new UnauthorizedAccessException(model.Path);
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
        public IActionResult WalletClose(
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
        public IActionResult WalletCreateNewAddress(
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
            return Ok(new WalletAddressModel()
            {
                Address = account.Address,
                ScriptHash = account.ScriptHash,
                PublicKey = account.GetKey().PublicKey,
                HasKey = account.HasKey,
                Label = account.Label,
                WatchOnly = account.WatchOnly,
            });
        }

        [HttpGet("{session:required}/balance/{asset:required}", Name = "WalletBalanceOf")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult WalletBalance(
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

        [HttpGet("{session:required}/gas/unclaimed", Name = "GetUnClaimedGas")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult WalletUnClaimedGas(
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
        public IActionResult WalletImportPrivateKey(
            [FromRoute(Name = "session")]
            Guid sessionId,
            [FromBody]
            WalletImportKey model)
        {
            if (WalletSessions.ContainsKey(sessionId) == false)
                throw new KeyNotFoundException(sessionId.ToString("n"));
            if (string.IsNullOrEmpty(model.Wif))
                throw new JsonPropertyNullOrEmptyException(nameof(model.Wif));
            var session = WalletSessions[sessionId];
            session.ResetExpiration();
            var wallet = session.Wallet;
            var account = wallet.Import(model.Wif);
            if (wallet is NEP6Wallet nep6)
                nep6.Save();
            return Ok(new WalletAddressModel()
            {
                Address = account.Address,
                ScriptHash = account.ScriptHash,
                PublicKey = account.GetKey().PublicKey,
                HasKey = account.HasKey,
                Label = account.Label,
                WatchOnly = account.WatchOnly,
            });
        }

        [HttpGet("{session:required}/address/list", Name = "GetWalletListAddress")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult WalletListAddresses(
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
                    ScriptHash = account.ScriptHash,
                    PublicKey = account.GetKey().PublicKey,
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
            if (model.AssetId == null || model.AssetId == UInt160.Zero)
                throw new JsonPropertyNullException(nameof(model.AssetId));
            if (model.From == null)
                throw new JsonPropertyNullException(nameof(model.From));
            if (model.To == null)
                throw new JsonPropertyNullException(nameof(model.To));
            var session = WalletSessions[sessionId];
            session.ResetExpiration();
            var wallet = session.Wallet;
            using var snapshot = _neosystem.GetSnapshot();
            try
            {
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
            catch (Exception ex)
            {
                throw ex.InnerException ?? ex;
            }
        }

        [HttpPost("create", Name = "WalletCreate")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult WalletCreate(
            [FromBody]
            WalletCreateModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Path))
                throw new JsonPropertyNullOrEmptyException(nameof(model.Path));
            if (string.IsNullOrEmpty(model.Password))
                throw new JsonPropertyNullOrEmptyException(nameof(model.Password));
            var wallet = Wallet.Create(model.Name, model.Path, model.Password, _neosystem.Settings);
            if (wallet == null)
                throw new WalletException("Wallet files in that format are not supported, please use a .json or .db3 file extension.");
            if (string.IsNullOrEmpty(model.Wif) == false)
                wallet.Import(model.Wif);
            wallet.Save();
            var sessionId = Guid.NewGuid();
            WalletSessions[sessionId] = new WalletSession(wallet);
            return Ok(new
            {
                SessionId = sessionId.ToString("n"),
            });
        }

        [HttpPost("{session:required}/import/multisigaddress", Name = "WalletImportMultiSigAddress")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult WalletImportMultiSigAddress(
            [FromRoute(Name = "session")]
            Guid sessionId,
            [FromBody]
            WalletImportMultiSigAddressModel model)
        {
            if (WalletSessions.ContainsKey(sessionId) == false)
                throw new KeyNotFoundException(sessionId.ToString("n"));
            if (model.PublicKeys == null || model.PublicKeys.Length == 0)
                throw new WalletException($"{nameof(model.PublicKeys)} is invalid.");
            var session = WalletSessions[sessionId];
            var wallet = session.Wallet;
            session.ResetExpiration();

            int n = model.PublicKeys.Length;

            if (model.RequiredSignatures < 1 || model.RequiredSignatures > n || n > 1024)
                throw new WalletException($"{nameof(model.RequiredSignatures)} and {nameof(model.PublicKeys)} is invalid.");

            Contract multiSignContract = Contract.CreateMultiSigContract(model.RequiredSignatures, model.PublicKeys);
            KeyPair keyPair = wallet.GetAccounts().FirstOrDefault(p => p.HasKey && model.PublicKeys.Contains(p.GetKey().PublicKey))?.GetKey();

            wallet.CreateAccount(multiSignContract, keyPair);
            if (wallet is NEP6Wallet nep6)
                nep6.Save();
            return Ok(new
            {
                Address = multiSignContract.ScriptHash.ToAddress(_neosystem.Settings.AddressVersion),
                multiSignContract.ScriptHash,
                multiSignContract.Script,
            });
        }

        [HttpGet("{session:required}/asset/list", Name = "GetWalletAssetList")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult WalletListAsset(
            [FromRoute(Name = "session")]
            Guid sessionId)
        {
            if (WalletSessions.ContainsKey(sessionId) == false)
                throw new KeyNotFoundException(sessionId.ToString("n"));
            var session = WalletSessions[sessionId];
            var wallet = session.Wallet;
            session.ResetExpiration();
            var assets = new List<WalletAssetModel>();
            foreach (var account in wallet.GetAccounts())
                assets.Add(new()
                {
                    Address = account.Address,
                    ScriptHash = account.ScriptHash,
                    PublicKey = account.GetKey().PublicKey,
                    Neo = wallet.GetBalance(_neosystem.StoreView, NativeContract.NEO.Hash, account.ScriptHash),
                    NeoHash = NativeContract.NEO.Hash,
                    Gas = wallet.GetBalance(_neosystem.StoreView, NativeContract.GAS.Hash, account.ScriptHash),
                    GasHash = NativeContract.GAS.Hash,
                });
            return Ok(assets);
        }

        [HttpGet("{session:required}/key/list", Name = "GetWalletKeyList")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult WalletListKeys(
            [FromRoute(Name = "session")]
            Guid sessionId)
        {
            if (WalletSessions.ContainsKey(sessionId) == false)
                throw new KeyNotFoundException(sessionId.ToString("n"));
            var session = WalletSessions[sessionId];
            var wallet = session.Wallet;
            session.ResetExpiration();
            var keys = new List<WalletKeyModel>();
            foreach (var account in wallet.GetAccounts().Where(w => w.HasKey))
                keys.Add(new()
                {
                    Address = account.Address,
                    ScriptHash = account.ScriptHash,
                    PublicKey = account.GetKey().PublicKey,
                });
            return Ok(keys);
        }

        [HttpPost("{session:required}/changepassword", Name = "WalletChangePassword")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult WalletChangePassword(
            [FromRoute(Name = "session")]
            Guid sessionId,
            [FromBody]
            WalletChangePasswordModel model)
        {
            if (WalletSessions.ContainsKey(sessionId) == false)
                throw new KeyNotFoundException(sessionId.ToString("n"));
            if (string.IsNullOrEmpty(model.OldPassword))
                throw new JsonPropertyNullOrEmptyException(nameof(model.OldPassword));
            if (string.IsNullOrEmpty(model.NewPassword))
                throw new JsonPropertyNullOrEmptyException(nameof(model.NewPassword));
            if (model.OldPassword == model.NewPassword)
                throw new WalletException($"{nameof(model.OldPassword)} is the same as {nameof(model.NewPassword)}.");
            var session = WalletSessions[sessionId];
            var wallet = session.Wallet;
            session.ResetExpiration();
            if (wallet.VerifyPassword(model.OldPassword) == false)
                throw new WalletException("Invalid password! Session terminated!");
            if (model.CreateBackupFile && wallet is NEP6Wallet)
            {
                var bakFile = wallet.Path + $".{sessionId:n}.bak";
                if (System.IO.File.Exists(wallet.Path) == false || System.IO.File.Exists(bakFile))
                    throw new WalletException("Wallet backup failed.");
                System.IO.File.Copy(wallet.Path, bakFile, model.OverwriteIfBackupFileExists);
            }
            if (wallet.ChangePassword(model.OldPassword, model.NewPassword) == false)
                throw new WalletException("Failed to change password!");
            if (wallet is NEP6Wallet nep6)
                nep6.Save();
            return Ok();
        }

        [HttpPost("{session:required}/transaction/script", Name = "WalletTransactionWithScript")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult WalletTransactionWithScript(
            [FromRoute(Name = "session")]
            Guid sessionId,
            [FromBody]
            WalletTransactionScriptModel model)
        {
            if (WalletSessions.ContainsKey(sessionId) == false)
                throw new KeyNotFoundException(sessionId.ToString("n"));
            if (model.Script == null || model.Script.Length == 0)
                throw new JsonPropertyNullOrEmptyException(nameof(model.Script));
            if (model.From == null)
                throw new JsonPropertyNullException(nameof(model.From));
            var session = WalletSessions[sessionId];
            session.ResetExpiration();
            var wallet = session.Wallet;
            var signers = model.Signers?.Select(s => new Signer() { Scopes = WitnessScope.CalledByEntry, Account = s }).ToArray();
            var appEngine = ScriptHelper.InvokeScript(_settings, model.Script, signers);
            if (appEngine.State != VM.VMState.HALT)
                throw new ApplicationEngineException(appEngine.FaultException?.InnerException?.Message ?? appEngine.FaultException?.Message ?? string.Empty);
            var tx = wallet.MakeTransaction(_neosystem.StoreView, model.Script, model.From, signers, maxGas: _settings.MaxInvokeGas);
            try
            {
                var context = new ContractParametersContext(_neosystem.StoreView, tx, _neosystem.Settings.Network);
                wallet.Sign(context);
                if (context.Completed == false)
                    throw new WalletException($"Incomplete signature: {context}");
                else
                {
                    tx.Witnesses = context.GetWitnesses();
                    _neosystem.Blockchain.Tell(tx);
                    return Ok(tx);
                }
            }
            catch (Exception ex)
            {
                throw ex.InnerException ?? ex;
            }
        }
    }
}
