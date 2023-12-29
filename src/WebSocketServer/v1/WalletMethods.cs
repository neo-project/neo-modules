using Akka.Actor;
using Neo.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.Wallets;
using System;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using static Neo.Ledger.Blockchain;

namespace Neo.Plugins.WebSocketServer.v1;

internal class WalletMethods
{
    private readonly NeoSystem _neoSystem;
    private readonly WalletSessionManager _walletSessionManager;

    public WalletMethods(
            NeoSystem neoSystem)
    {
        _neoSystem = neoSystem;
        _walletSessionManager = new();
        WebSocketServerPlugin.RegisterMethods(this);
    }

    [WebSocketMethod]
    public JToken WalletOpen(JArray _params)
    {
        if (_params.Count != 2)
            throw new WebSocketException(-32602, "Invalid params");

        var walletFilename = _params[0].AsString();
        var walletFilePassword = _params[1].AsString();

        if (File.Exists(walletFilename) == false)
            throw new WebSocketException(-100, $"File '{walletFilename}' could not be found.");

        var wallet = Wallet.Open(walletFilename, walletFilePassword, _neoSystem.Settings);
        if (wallet == null)
            throw new WebSocketException(-100, $"Wallet '{walletFilename}' could not be opened.");

        var sessionId = Guid.NewGuid();

        _walletSessionManager[sessionId] = new(wallet);

        return new JObject()
        {
            ["sessionid"] = $"{sessionId:n}",
        };
    }

    [WebSocketMethod]
    public JToken WalletClose(JArray _params)
    {
        if (_params.Count != 1)
            throw new WebSocketException(-32602, "Invalid params");

        if (Guid.TryParse(_params[0].AsString(), out var sessionId))
        {
            if (_walletSessionManager.ContainsKey(sessionId) == false)
                throw new WebSocketException(-100, "Invalid session id");
            if (_walletSessionManager.TryRemove(sessionId, out _) == false)
                throw new WebSocketException(-100, "Failed to remove session");

            return new JObject()
            {
                ["successful"] = true,
            };
        }
        else
            throw new WebSocketException(-32602, "Invalid params");
    }

    [WebSocketMethod]
    public JToken WalletExport(JArray _params)
    {
        if (_params.Count != 1)
            throw new WebSocketException(-32602, "Invalid params");

        if (Guid.TryParse(_params[0].AsString(), out var sessionId))
        {
            if (_walletSessionManager.ContainsKey(sessionId) == false)
                throw new WebSocketException(-100, "Invalid session id");

            var walletSession = _walletSessionManager[sessionId];
            walletSession.ResetExpiration();

            var wallet = walletSession.Wallet;
            var walletAddresses = wallet.GetAccounts()
                .Where(w => w.HasKey)
                .Select(s => new JObject()
                {
                    ["scripthash"] = $"{s.ScriptHash}",
                    ["address"] = s.Address,
                    ["wif"] = s.GetKey().Export(),
                });

            return new JArray(walletAddresses);
        }
        else
            throw new WebSocketException(-32602, "Invalid params");
    }

    [WebSocketMethod]
    public JToken WalletCreateNewAccount(JArray _params)
    {
        if (_params.Count == 0)
            throw new WebSocketException(-32602, "Invalid params");

        if (Guid.TryParse(_params[0].AsString(), out var sessionId))
        {
            if (_walletSessionManager.ContainsKey(sessionId) == false)
                throw new WebSocketException(-100, "Invalid session id");

            var walletSession = _walletSessionManager[sessionId];
            walletSession.ResetExpiration();

            var wifString = _params.Count == 2 ?
                _params[1].AsString() :
                string.Empty;

            var wallet = walletSession.Wallet;
            var walletNewAccount = string.IsNullOrEmpty(wifString) ?
                wallet.CreateAccount() :
                wallet.CreateAccount(Wallet.GetPrivateKeyFromWIF(wifString));

            if (walletNewAccount == null)
                throw new WebSocketException(-100, "Account couldn't be created");

            wallet.Save();

            var json = new JObject()
            {
                ["scripthash"] = $"{walletNewAccount.ScriptHash}",
                ["address"] = walletNewAccount.Address,
                ["publickey"] = $"{walletNewAccount.GetKey().PublicKey}",
            };

            if (string.IsNullOrEmpty(wifString))
                json["wif"] = walletNewAccount.GetKey().Export();

            return json;
        }
        else
            throw new WebSocketException(-32602, "Invalid params");
    }

    [WebSocketMethod]
    public JToken WalletBalance(JArray _params)
    {
        if (_params.Count != 3)
            throw new WebSocketException(-32602, "Invalid params");

        if (Guid.TryParse(_params[0].AsString(), out var sessionId))
        {
            var walletAddress = WebSocketUtility.TryParseScriptHash(_params[1].AsString(), _neoSystem.Settings.AddressVersion);
            var walletAssetScriptHash = WebSocketUtility.TryParseScriptHash(_params[2].AsString(), _neoSystem.Settings.AddressVersion);

            if (walletAddress == UInt160.Zero || walletAssetScriptHash == UInt160.Zero)
                throw new WebSocketException(-32602, "Invalid params");

            if (_walletSessionManager.ContainsKey(sessionId) == false)
                throw new WebSocketException(-100, "Invalid session id");

            var walletSession = _walletSessionManager[sessionId];
            walletSession.ResetExpiration();

            var wallet = walletSession.Wallet;
            var walletAccount = wallet.GetAccount(walletAddress);

            if (walletAccount == null)
                throw new WebSocketException(-100, "Account not found");

            var accountBalance = wallet.GetBalance(_neoSystem.StoreView, walletAssetScriptHash, walletAddress);

            return new JObject()
            {
                ["decimals"] = accountBalance.Decimals,
                // This should be in Big Number format for json. See: https://github.com/neo-project/neo/issues/3036
                ["balance"] = $"{accountBalance.Value}",
            };
        }
        else
            throw new WebSocketException(-32602, "Invalid params");
    }

    [WebSocketMethod]
    public JToken WalletUnClaimedGas(JArray _params)
    {
        if (_params.Count != 2)
            throw new WebSocketException(-32602, "Invalid params");

        if (Guid.TryParse(_params[0].AsString(), out var sessionId))
        {
            var walletAddress = WebSocketUtility.TryParseScriptHash(_params[1].AsString(), _neoSystem.Settings.AddressVersion);

            if (walletAddress == UInt160.Zero)
                throw new WebSocketException(-32602, "Invalid params");

            if (_walletSessionManager.ContainsKey(sessionId) == false)
                throw new WebSocketException(-100, "Invalid session id");

            var walletSession = _walletSessionManager[sessionId];
            walletSession.ResetExpiration();

            var wallet = walletSession.Wallet;
            var walletAccount = wallet.GetAccount(walletAddress);

            if (walletAccount == null)
                throw new WebSocketException(-100, "Account not found");

            var blockHeight = NativeContract.Ledger.CurrentIndex(_neoSystem.StoreView) + 1;
            var unclaimedGas = NativeContract.NEO.UnclaimedGas(_neoSystem.StoreView, walletAddress, blockHeight);

            return new JObject()
            {
                ["blockheight"] = blockHeight,
                ["balance"] = $"{unclaimedGas}",
            };
        }
        else
            throw new WebSocketException(-32602, "Invalid params");
    }

    [WebSocketMethod]
    public JToken WalletImport(JArray _params)
    {
        if (_params.Count != 2)
            throw new WebSocketException(-32602, "Invalid params");

        if (Guid.TryParse(_params[0].AsString(), out var sessionId))
        {
            var wif = _params[1].AsString();

            if (string.IsNullOrEmpty(wif))
                throw new WebSocketException(-32602, "Invalid params");

            if (_walletSessionManager.ContainsKey(sessionId) == false)
                throw new WebSocketException(-100, "Invalid session id");

            var walletSession = _walletSessionManager[sessionId];
            walletSession.ResetExpiration();

            var wallet = walletSession.Wallet;
            var walletNewAccount = wallet.Import(wif);

            if (walletNewAccount == null)
                throw new WebSocketException(-100, "Import Failed");

            wallet.Save();

            return new JObject()
            {
                ["scripthash"] = $"{walletNewAccount.ScriptHash}",
                ["address"] = walletNewAccount.Address,
                ["publickey"] = $"{walletNewAccount.GetKey().PublicKey}",
            };
        }
        else
            throw new WebSocketException(-32602, "Invalid params");
    }

    [WebSocketMethod]
    public JToken WalletListAddresses(JArray _params)
    {
        if (_params.Count != 1)
            throw new WebSocketException(-32602, "Invalid params");

        if (Guid.TryParse(_params[0].AsString(), out var sessionId))
        {
            if (_walletSessionManager.ContainsKey(sessionId) == false)
                throw new WebSocketException(-100, "Invalid session id");

            var walletSession = _walletSessionManager[sessionId];
            walletSession.ResetExpiration();

            var wallet = walletSession.Wallet;
            var walletAccounts = wallet
                .GetAccounts()
                .Select(s => new JObject()
                {
                    ["scripthash"] = $"{s.ScriptHash}",
                    ["address"] = s.Address,
                    ["publickey"] = $"{s.GetKey().PublicKey}",
                    ["watchonly"] = s.WatchOnly,
                    ["label"] = s.Label,
                    ["haskey"] = s.HasKey,
                    ["isdefault"] = s.IsDefault,
                    ["islocked"] = s.Lock,
                });

            return new JArray(walletAccounts);
        }
        else
            throw new WebSocketException(-32602, "Invalid params");
    }

    [WebSocketMethod]
    public JToken WalletDeleteAccount(JArray _params)
    {
        if (_params.Count != 2)
            throw new WebSocketException(-32602, "Invalid params");

        if (Guid.TryParse(_params[0].AsString(), out var sessionId))
        {
            var walletAddress = WebSocketUtility.TryParseScriptHash(_params[1].AsString(), _neoSystem.Settings.AddressVersion);

            if (walletAddress == UInt160.Zero)
                throw new WebSocketException(-32602, "Invalid params");

            if (_walletSessionManager.ContainsKey(sessionId) == false)
                throw new WebSocketException(-100, "Invalid session id");

            var walletSession = _walletSessionManager[sessionId];
            walletSession.ResetExpiration();

            var wallet = walletSession.Wallet;

            if (wallet.DeleteAccount(walletAddress) == false)
                throw new WebSocketException(-100, "Delete failed");

            wallet.Save();

            return new JObject()
            {
                ["successful"] = true,
            };
        }
        else
            throw new WebSocketException(-32602, "Invalid params");
    }

    [WebSocketMethod]
    public JToken WalletTransferAssets(JArray _params)
    {
        if (_params.Count != 5)
            throw new WebSocketException(-32602, "Invalid params");

        if (Guid.TryParse(_params[0].AsString(), out var sessionId))
        {
            var assetId = WebSocketUtility.TryParseScriptHash(_params[1].AsString(), _neoSystem.Settings.AddressVersion);
            var walletFromAddress = WebSocketUtility.TryParseScriptHash(_params[2].AsString(), _neoSystem.Settings.AddressVersion);
            var WalletToAddress = WebSocketUtility.TryParseScriptHash(_params[3].AsString(), _neoSystem.Settings.AddressVersion);
            var amount = WebSocketUtility.TryParseBigInteger(_params[4].AsString());
            var data = _params.Count >= 6 ? Convert.FromBase64String(_params[5].AsString()) : null;
            var signers = _params.Count >= 7 ? ((JArray)_params[6])
                .Select(s => new Signer()
                {
                    Account = WebSocketUtility.TryParseScriptHash(s.AsString(), _neoSystem.Settings.AddressVersion),
                    Scopes = WitnessScope.CalledByEntry,
                }) : null;

            if (walletFromAddress == UInt160.Zero ||
                WalletToAddress == UInt160.Zero ||
                amount.Sign < 0)
                throw new WebSocketException(-32602, "Invalid params");

            if (_walletSessionManager.ContainsKey(sessionId) == false)
                throw new WebSocketException(-100, "Invalid session id");

            var walletSession = _walletSessionManager[sessionId];
            walletSession.ResetExpiration();

            var wallet = walletSession.Wallet;
            var assetDescriptor = new AssetDescriptor(_neoSystem.StoreView, _neoSystem.Settings, assetId);
            var tx = wallet.MakeTransaction(_neoSystem.StoreView,
                new[]
                {
                    new TransferOutput()
                    {
                        AssetId = assetId,
                        Value = new BigDecimal(amount, assetDescriptor.Decimals),
                        ScriptHash = WalletToAddress,
                        Data = data,
                    },
                }, walletFromAddress, signers.ToArray());

            if (tx == null)
                throw new WebSocketException(-100, "Transaction failed");

            var context = new ContractParametersContext(_neoSystem.StoreView, tx, _neoSystem.Settings.Network);

            wallet.Sign(context);

            if (context.Completed == false)
                throw new WebSocketException(-100, "Transaction failed");

            tx.Witnesses = context.GetWitnesses();
            var txResult = (RelayResult)_neoSystem.Blockchain.Ask(tx).Result;

            if (txResult.Result == VerifyResult.Succeed)
                return tx.ToJson(_neoSystem.Settings);

            throw new WebSocketException(-100, $"Transaction {txResult.Result}");
        }
        else
            throw new WebSocketException(-32602, "Invalid params");
    }
}
