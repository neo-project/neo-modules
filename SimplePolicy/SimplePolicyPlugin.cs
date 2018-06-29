using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Plugins
{
    public class SimplePolicyPlugin : PolicyPlugin
    {
        public override string Name => nameof(SimplePolicyPlugin);

        protected override bool CheckPolicy(Transaction tx)
        {
            switch (Settings.Default.BlockedAccounts.Type)
            {
                case PolicyType.AllowAll:
                    return true;
                case PolicyType.AllowList:
                    return tx.Witnesses.All(p => Settings.Default.BlockedAccounts.List.Contains(p.VerificationScript.ToScriptHash())) || tx.Outputs.All(p => Settings.Default.BlockedAccounts.List.Contains(p.ScriptHash));
                case PolicyType.DenyList:
                    return tx.Witnesses.All(p => !Settings.Default.BlockedAccounts.List.Contains(p.VerificationScript.ToScriptHash())) && tx.Outputs.All(p => !Settings.Default.BlockedAccounts.List.Contains(p.ScriptHash));
                default:
                    return false;
            }
        }

        protected override IEnumerable<Transaction> Filter(IEnumerable<Transaction> transactions)
        {
            Transaction[] array = transactions.ToArray();
            if (array.Length + 1 <= Settings.Default.MaxTransactionsPerBlock)
                return array;
            transactions = array.OrderByDescending(p => p.NetworkFee / p.Size).Take(Settings.Default.MaxTransactionsPerBlock - 1);
            return FilterFree(transactions);
        }

        private IEnumerable<Transaction> FilterFree(IEnumerable<Transaction> transactions)
        {
            int count = 0;
            foreach (Transaction tx in transactions)
                if (tx.NetworkFee > Fixed8.Zero || tx.SystemFee > Fixed8.Zero)
                    yield return tx;
                else if (count++ < Settings.Default.MaxFreeTransactionsPerBlock)
                    yield return tx;
                else
                    yield break;
        }
    }
}
