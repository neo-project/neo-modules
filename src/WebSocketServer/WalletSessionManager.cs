// Copyright (C) 2015-2024 The Neo Project.
//
// WalletSessionManager.cs file belongs to the neo project and is free
// software distributed under the MIT software license, see the
// accompanying file LICENSE in the main directory of the
// repository or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Plugins.WsRpcJsonServer
{
    public class WalletSessionManager : ConcurrentDictionary<Guid, WalletSession>
    {
        private readonly PeriodicTimer _timer;

        public WalletSessionManager()
        {
            _timer = new(TimeSpan.FromSeconds(1));
            _ = Task.Run(SessionTimeoutAsync);
        }

        private async Task SessionTimeoutAsync()
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
}
