using Neo.Consensus;
using Neo.Network.P2P.Payloads;
using Neo.SmartContract;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Neo.Plugins
{
    public class SimplePolicyPlugin : Plugin, ILogPlugin, IPolicyPlugin
    {
        private static string log_dictionary = Path.Combine(AppContext.BaseDirectory, "Logs");

        public bool FilterForMemoryPool(Transaction tx)
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

        public IEnumerable<Transaction> FilterForBlock(IEnumerable<Transaction> transactions)
        {
            Transaction[] array = transactions.ToArray();
            if (array.Length + 1 <= Settings.Default.MaxTransactionsPerBlock)
                return array;
            transactions = array.OrderByDescending(p => p.NetworkFee / p.Size).ThenByDescending(p => p.NetworkFee).Take(Settings.Default.MaxTransactionsPerBlock - 1);
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
        }

        void ILogPlugin.Log(string source, LogLevel level, string message)
        {
            if (source != nameof(ConsensusService)) return;
            DateTime now = DateTime.Now;
            string line = $"[{now.TimeOfDay:hh\\:mm\\:ss\\.fff}] {message}";
            Console.WriteLine(line);
            if (string.IsNullOrEmpty(log_dictionary)) return;
            lock (log_dictionary)
            {
                Directory.CreateDirectory(log_dictionary);
                string path = Path.Combine(log_dictionary, $"{now:yyyy-MM-dd}.log");
                File.AppendAllLines(path, new[] { line });
            }
        }
    }
}
