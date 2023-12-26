using Neo.Json;
using Neo.Wallets;
using System;
using System.IO;
using System.Linq;
using System.Net.WebSockets;

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

    public JToken WalletBalance(JArray _params)
    {
        if (_params.Count != 3)
            throw new WebSocketException(-32602, "Invalid params");

        if (Guid.TryParse(_params[0].AsString(), out var sessionId))
        {
            UInt160 walletAddress = WebSocketUtility.TryParseScriptHash(_params[1].AsString(), _neoSystem.Settings.AddressVersion);
            UInt160 walletAssetScriptHash = WebSocketUtility.TryParseScriptHash(_params[2].AsString(), _neoSystem.Settings.AddressVersion);

            if (walletAddress == UInt160.Zero)
                throw new WebSocketException(-100, "Invalid params");

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
}
