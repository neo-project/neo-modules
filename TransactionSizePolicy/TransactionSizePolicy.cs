using System.Collections.Generic;
using Neo.Network.P2P.Payloads;

namespace Neo.Plugins
{
    public class TransactionSizePolicyPlugin : Plugin, IPolicyPlugin
    {
        public bool CheckPolicy(Transaction tx)
        {
            return VerifySizeLimits(tx);
        }

        public IEnumerable<Transaction> Filter(IEnumerable<Transaction> transactions) => transactions;
        
        private bool VerifySizeLimits(Transaction tx)
        {
            // Not Allow free TX bigger than MaxFreeTransactionSize
            if (tx.NetworkFee.Equals(0) && tx.Size > Settings.Default.MaxFreeTransactionSize) return false;

            // Not Allow TX bigger than MaxTransactionSize
            if (tx.Size > Settings.Default.MaxTransactionSize) return false;

            // For TX bigger than TransactionExtraSize require proportional fee
            if (tx.Size > Settings.Default.TransactionExtraSize)
            {
                decimal fee = (tx.Size - Settings.Default.TransactionExtraSize) * Settings.Default.FeePerExtraByte;

                if (tx.NetworkFee < Fixed8.FromDecimal(fee)) return false;
            }
            return true;
        }
    }
}