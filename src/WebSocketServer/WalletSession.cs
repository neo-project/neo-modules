// Copyright (C) 2015-2024 The Neo Project.
//
// WalletSession.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Wallets;
using System;

namespace Neo.Plugins
{
    public class WalletSession
    {
        public Wallet Wallet { get; private init; }
        public DateTime Expires { get; private set; }

        public WalletSession(
            Wallet wallet)
        {
            Wallet = wallet;
            ResetExpiration();
        }

        public void ResetExpiration() =>
            Expires = DateTime.UtcNow.AddSeconds(WebSocketServerSettings.Current?.WalletSessionTimeout ?? WebSocketServerSettings.Default.WalletSessionTimeout);
    }
}
