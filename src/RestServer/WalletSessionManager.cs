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
        private readonly PeriodicTimer _timer;

        public WalletSessionManager()
        {
            _timer = new(TimeSpan.FromSeconds(1));
            _ = Task.Run(SessionTimeout);
        }

        private async Task SessionTimeout()
        {
            while (await _timer.WaitForNextTickAsync())
            {
                var killAll = this.Where(w => w.Value.Expires <= DateTime.UtcNow)
                    .Select(s => Task.Run(() =>
                    {
                        TryRemove(s);
                    }));
                await Task.WhenAll(killAll);
            }
        }
    }

    public class WalletSession
    {
        public Wallet Wallet { get; private init; }

        /// <summary>
        /// Expiration DateTime in UTC
        /// </summary>
        public DateTime Expires { get; private set; }

        public WalletSession(Wallet wallet)
        {
            Wallet = wallet;
            ResetExpiration();
        }

        public void ResetExpiration() =>
            Expires = DateTime.UtcNow.AddSeconds(RestServerSettings.Current.WalletSessionTimeout);
    }
}
