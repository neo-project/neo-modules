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
