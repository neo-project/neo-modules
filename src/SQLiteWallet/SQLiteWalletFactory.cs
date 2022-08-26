// Copyright (C) 2015-2022 The Neo Project.
// 
// The Neo.Wallets.SQLite is free software distributed under the MIT software license, 
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php 
// for more details.
// 
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Plugins;
using static System.IO.Path;

namespace Neo.Wallets.SQLite;

public class SQLiteWalletFactory : Plugin, IWalletFactory
{
    public SQLiteWalletFactory()
    {
        Wallet.RegisterFactory(this);
    }

    public bool Handle(string path)
    {
        return GetExtension(path).ToLowerInvariant() == ".db3";
    }

    public Wallet CreateWallet(string name, string path, string password, ProtocolSettings settings)
    {
        return SQLiteWallet.Create(path, password, settings);
    }

    public Wallet OpenWallet(string path, string password, ProtocolSettings settings)
    {
        return SQLiteWallet.Open(path, password, settings);
    }
}
