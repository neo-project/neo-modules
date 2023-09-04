// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Wallets;
using System.Collections.Concurrent;

namespace Neo.Plugins.RestServer
{
    public class WalletSessionManager : ConcurrentDictionary<Guid, WalletSession>
    {
        private readonly Timer _timer;

        public WalletSessionManager()
        {
            _timer = new(SessionTimeout, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        }

        private void SessionTimeout(object data)
        {
            var killAll = this.Where(w => w.Value.Expires <= DateTime.Now)
                .Select(s => Task.Run(() =>
                {
                    TryRemove(s);
                }));
            Task.WhenAll(killAll);
        }
    }

    public class WalletSession
    {
        public Wallet Wallet { get; private init; }
        public DateTime Expires { get; private set; }

        public WalletSession(Wallet wallet)
        {
            Wallet = wallet;
            ResetExpiration();
        }

        public void ResetExpiration() =>
            Expires = DateTime.Now.AddSeconds(RestServerSettings.Current.WalletSessionTimeout);
    }
}
