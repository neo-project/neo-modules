using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Plugins
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
