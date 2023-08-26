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
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Neo.Network.P2P.Payloads;
using Neo.Plugins.RestServer.Exceptions;
using Neo.Plugins.RestServer.Extensions;
using Neo.Plugins.RestServer.Helpers;
using Neo.Plugins.RestServer.Models.Error;
using Neo.Plugins.RestServer.Models.Wallet;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.Wallets;
using Neo.Wallets.NEP6;
using System.Net.Mime;
using System.Numerics;

namespace Neo.Plugins.RestServer.Controllers
{
    /// <summary>
    /// Wallet API
    /// </summary>
    [Route("/api/v1/wallet")]
    [DisableCors]
    [Produces(MediaTypeNames.Application.Json)]
    [Consumes(MediaTypeNames.Application.Json)]
    [ApiExplorerSettings(GroupName = "v1")]
    [ApiController]
    public class WalletController : ControllerBase
    {
        internal static WalletSessionManager WalletSessions { get; } = new();

        private readonly NeoSystem _neosystem;
        private readonly RestServerSettings _settings;

        /// <summary>
        /// CTOR
        /// </summary>
        /// <exception cref="NodeNetworkException">Node network doesn't match plugins network.</exception>
        public WalletController()
        {
            _neosystem = RestServerPlugin.NeoSystem ?? throw new NodeNetworkException();
            _settings = RestServerSettings.Current;
        }

        /// <summary>
        /// Opens a wallet.
        /// </summary>
        /// <param name="model"></param>
        /// <returns>A newly created wallet session object.</returns>
        /// <response code="200">Returns newly create wallet session object.</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpPost("open", Name = "WalletOpen")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WalletSessionModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
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
            return Ok(new WalletSessionModel()
            {
                SessionId = sessionId,
            });
        }

        /// <summary>
        /// Closes a wallet session.
        /// </summary>
        /// <param name="sessionId" example="066843daf5ce45aba803587780998cdb">Session Id of the open/created wallet.</param>
        /// <returns>Empty response body, if successful.</returns>
        /// <response code="200">Successfully closed the wallet session.</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpGet("{session:required}/close", Name = "WalletClose")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
        public IActionResult WalletClose(
            [FromRoute(Name = "session")]
            Guid sessionId)
        {
            if (WalletSessions.ContainsKey(sessionId) == false)
                throw new KeyNotFoundException(sessionId.ToString("n"));
            if (WalletSessions.TryRemove(sessionId, out _) == false)
                throw new WalletSessionException("Failed to remove session.");
            return Ok();
        }

        /// <summary>
        /// Get all the keys of the wallet from each account.
        /// </summary>
        /// <param name="sessionId" example="066843daf5ce45aba803587780998cdb">Session Id of the open/created wallet.</param>
        /// <returns>A list of export key objects.</returns>
        /// <response code="200">Successfully exported the keys.</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpGet("{session:required}/export", Name = "WalletExportKeys")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WalletExportKeyModel[]))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
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

        /// <summary>
        /// Get a key of the wallet by a specific account.
        /// </summary>
        /// <param name="sessionId" example="066843daf5ce45aba803587780998cdb">Session Id of the open/created wallet.</param>
        /// <param name="scriptHash" example="0xed7cc6f5f2dd842d384f254bc0c2d58fb69a4761">ScriptHash of the wallet address.</param>
        /// <returns>A export key object.</returns>
        /// <response code="200">Successfully exported the key.</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpGet("{session:required}/export/{address:required}", Name = "WalletExportKeysByAddressOrScripthash")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WalletExportKeyModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
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

        /// <summary>
        /// Create a new address in the wallet with an optional private key.
        /// </summary>
        /// <param name="sessionId" example="066843daf5ce45aba803587780998cdb">Session Id of the open/created wallet.</param>
        /// <param name="model"></param>
        /// <returns>Wallet address object.</returns>
        /// <response code="200">Successfully created address.</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpPost("{session:required}/address/create", Name = "WalletCreateAddress")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WalletAddressModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
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
                Publickey = account.GetKey().PublicKey,
                HasKey = account.HasKey,
                Label = account.Label,
                WatchOnly = account.WatchOnly,
            });
        }

        /// <summary>
        /// Get the wallet balance of a specific asset.
        /// </summary>
        /// <param name="sessionId" example="066843daf5ce45aba803587780998cdb">Session Id of the open/created wallet.</param>
        /// <param name="scriptHash" example="0xed7cc6f5f2dd842d384f254bc0c2d58fb69a4761">ScriptHash of the wallet address.</param>
        /// <returns>Account balance object of all the accounts in the wallet.</returns>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpGet("{session:required}/balance/{asset:required}", Name = "WalletBalanceOf")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WalletAccountBalanceModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
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
            return Ok(new WalletAccountBalanceModel()
            {
                Balance = balance.Value,
            });
        }

        /// <summary>
        /// Get unclaimed gas of the wallet for all accounts total.
        /// </summary>
        /// <param name="sessionId" example="066843daf5ce45aba803587780998cdb">Session Id of the open/created wallet.</param>
        /// <returns>Account balance object</returns>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpGet("{session:required}/gas/unclaimed", Name = "GetUnClaimedGas")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WalletAccountBalanceModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
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
            return Ok(new WalletAccountBalanceModel
            {
                Balance = gas,
            });
        }

        /// <summary>
        /// import a private key into the wallet.
        /// </summary>
        /// <param name="sessionId" example="066843daf5ce45aba803587780998cdb">Session Id of the open/created wallet.</param>
        /// <param name="model"></param>
        /// <returns>New wallet address object.</returns>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpPost("{session:required}/import", Name = "WalletImportByWif")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WalletAddressModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
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
                Publickey = account.GetKey().PublicKey,
                HasKey = account.HasKey,
                Label = account.Label,
                WatchOnly = account.WatchOnly,
            });
        }

        /// <summary>
        /// List all the addresses in the wallet.
        /// </summary>
        /// <param name="sessionId" example="066843daf5ce45aba803587780998cdb">Session Id of the open/created wallet.</param>
        /// <returns>An array of wallet address objects.</returns>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpGet("{session:required}/address/list", Name = "GetWalletListAddress")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WalletAddressModel[]))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
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
                    Publickey = account.GetKey().PublicKey,
                    HasKey = account.HasKey,
                    Label = account.Label,
                    WatchOnly = account.WatchOnly,
                });
            return Ok(accounts);
        }

        /// <summary>
        /// Deletes an account from the wallet.
        /// </summary>
        /// <param name="sessionId" example="066843daf5ce45aba803587780998cdb">Session Id of the open/created wallet.</param>
        /// <param name="scriptHash" example="0xed7cc6f5f2dd842d384f254bc0c2d58fb69a4761">ScriptHash of the wallet address.</param>
        /// <returns>Empty body response.</returns>
        /// <remarks>No backups are made.</remarks>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpGet("{session:required}/delete/{account:required}", Name = "WalletDeleteAccountByAddressOrScriptHash")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
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

        /// <summary>
        /// Trasnsfer assets from one wallet address to another address on the blockchain.
        /// </summary>
        /// <param name="sessionId" example="066843daf5ce45aba803587780998cdb">Session Id of the open/created wallet.</param>
        /// <param name="model"></param>
        /// <returns>Transaction object</returns>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpPost("{session:required}/transfer", Name = "WalletTransferAssets")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
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

        /// <summary>
        /// Create a wallet.
        /// </summary>
        /// <param name="model"></param>
        /// <returns>A wallet session object.</returns>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpPost("create", Name = "WalletCreate")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WalletSessionModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
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
            return Ok(new WalletSessionModel()
            {
                SessionId = sessionId,
            });
        }

        /// <summary>
        /// Import multi-signature addresss into the wallet.
        /// </summary>
        /// <param name="sessionId" example="066843daf5ce45aba803587780998cdb">Session Id of the open/created wallet.</param>
        /// <param name="model"></param>
        /// <returns></returns>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpPost("{session:required}/import/multisigaddress", Name = "WalletImportMultiSigAddress")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WalletMultiSignContractModel))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
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
            return Ok(new WalletMultiSignContractModel
            {
                Address = multiSignContract.ScriptHash.ToAddress(_neosystem.Settings.AddressVersion),
                ScriptHash = multiSignContract.ScriptHash,
                Script = multiSignContract.Script,
            });
        }

        /// <summary>
        /// List assets of the wallet.
        /// </summary>
        /// <param name="sessionId" example="066843daf5ce45aba803587780998cdb">Session Id of the open/created wallet.</param>
        /// <returns>An array of wallet asset objects.</returns>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpGet("{session:required}/asset/list", Name = "GetWalletAssetList")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WalletAssetModel[]))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
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
                    Neo = wallet.GetBalance(_neosystem.StoreView, NativeContract.NEO.Hash, account.ScriptHash).Value,
                    NeoHash = NativeContract.NEO.Hash,
                    Gas = wallet.GetBalance(_neosystem.StoreView, NativeContract.GAS.Hash, account.ScriptHash).Value,
                    GasHash = NativeContract.GAS.Hash,
                });
            return Ok(assets);
        }

        /// <summary>
        /// List all account keys in the wallet.
        /// </summary>
        /// <param name="sessionId" example="066843daf5ce45aba803587780998cdb">Session Id of the open/created wallet.</param>
        /// <returns>An array of wallet key objects.</returns>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpGet("{session:required}/key/list", Name = "GetWalletKeyList")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(WalletKeyModel[]))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
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

        /// <summary>
        /// Change wallet password.
        /// </summary>
        /// <param name="sessionId" example="066843daf5ce45aba803587780998cdb">Session Id of the open/created wallet.</param>
        /// <param name="model"></param>
        /// <returns>Empty body response.</returns>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpPost("{session:required}/changepassword", Name = "WalletChangePassword")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
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

        /// <summary>
        /// Create a transaction with a script.
        /// </summary>
        /// <param name="sessionId" example="066843daf5ce45aba803587780998cdb">Session Id of the open/created wallet.</param>
        /// <param name="model"></param>
        /// <returns>Transaction object.</returns>
        /// <response code="200">Successful</response>
        /// <response code="400">If anything is invalid or request crashes.</response>
        [HttpPost("{session:required}/transaction/script", Name = "WalletTransactionWithScript")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Transaction))]
        [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ErrorModel))]
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
