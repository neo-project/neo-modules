using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Cron.Consensus;
using Cron.Ledger;
using Cron.Network.P2P.Payloads;
using Cron.SmartContract;
using Cron.IO.Json;
using Cron.Wallets;

[assembly: InternalsVisibleTo("SimplePolicy.UnitTests")]

namespace Cron.Plugins
{
    public class SimplePolicyPlugin : Plugin, ILogPlugin, IPolicyPlugin, IRpcPlugin
    {
        private static readonly string log_dictionary = Path.Combine(AppContext.BaseDirectory, "Logs");

        public override void Configure()
        {
            Settings.Load(GetConfiguration());
        }

        public bool FilterForMemoryPool(Transaction tx)
        {
            if (!VerifySizeLimits(tx)) return false;

            switch (Settings.Default.BlockedAccounts.Type)
            {
                case PolicyType.AllowAll:
                    return true;
                case PolicyType.AllowList:
                    return tx.Witnesses.All(p => Settings.Default.BlockedAccounts.List.Contains(p.VerificationScript.ToScriptHash())) ||
                            tx.Outputs.All(p => Settings.Default.BlockedAccounts.List.Contains(p.ScriptHash));
                case PolicyType.DenyList:
                    return tx.Witnesses.All(p => !Settings.Default.BlockedAccounts.List.Contains(p.VerificationScript.ToScriptHash())) &&
                            tx.Outputs.All(p => !Settings.Default.BlockedAccounts.List.Contains(p.ScriptHash));
                default:
                    return false;
            }
        }

        public IEnumerable<Transaction> FilterForBlock(IEnumerable<Transaction> transactions)
        {
            return FilterForBlock_Policy2(transactions);
        }

        public int MaxTxPerBlock => Settings.Default.MaxTransactionsPerBlock;
        public int MaxLowPriorityTxPerBlock => Settings.Default.MaxFreeTransactionsPerBlock;

        private static IEnumerable<Transaction> FilterForBlock_Policy1(IEnumerable<Transaction> transactions)
        {
            int count = 0, count_free = 0;
            foreach (Transaction tx in transactions.OrderByDescending(p => p.NetworkFee / p.Size).ThenByDescending(p => p.NetworkFee).ThenByDescending(p => InHigherLowPriorityList(p)))
            {
                if (count++ >= Settings.Default.MaxTransactionsPerBlock - 1) break;
                if (!tx.IsLowPriority || count_free++ < Settings.Default.MaxFreeTransactionsPerBlock)
                    yield return tx;
            }
        }

        private static IEnumerable<Transaction> FilterForBlock_Policy2(IEnumerable<Transaction> transactions)
        {
            if (!(transactions is IReadOnlyList<Transaction> tx_list))
                tx_list = transactions.ToArray();

            if (Blockchain.Singleton.Height < ProtocolSettings.Default.FreeGasChangeHeight)
            {
                Transaction[] free = tx_list.Where(p => p.IsLowPriority)
                .OrderByDescending(p => p.NetworkFee / p.Size)
                .ThenByDescending(p => p.NetworkFee)
                .ThenByDescending(p => InHigherLowPriorityList(p))
                .ThenBy(p => p.Hash)
                .Take(20)
                .ToArray();

                Transaction[] non_free = tx_list.Where(p => !p.IsLowPriority)
                .OrderByDescending(p => p.NetworkFee / p.Size)
                .ThenByDescending(p => p.NetworkFee)
                .ThenBy(p => p.Hash)
                .Take(479)
                .ToArray();

                return non_free.Concat(free);
            }
            else
            {
                Transaction[] non_free = tx_list.Where(p => !p.IsLowPriority)
                .OrderByDescending(p => p.NetworkFee / p.Size)
                .ThenByDescending(p => p.NetworkFee)
                .ThenBy(p => p.Hash)
                .Take(Settings.Default.MaxTransactionsPerBlock - 1)
                .ToArray();

                Transaction[] free = tx_list.Where(p => p.IsLowPriority)
                .OrderByDescending(p => p.NetworkFee / p.Size)
                .ThenByDescending(p => p.NetworkFee)
                .ThenByDescending(p => InHigherLowPriorityList(p))
                .ThenBy(p => p.Hash)
                .Take(Settings.Default.MaxTransactionsPerBlock - non_free.Length - 1)
                .ToArray();

                return non_free.Concat(free);
            }
        }

        void ILogPlugin.Log(string source, LogLevel level, string message)
        {
            if (source != nameof(ConsensusService)) return;
            DateTime now = DateTime.Now;
            string line = $"[{now.TimeOfDay:hh\\:mm\\:ss\\.fff}] {message}";
            Logger.Info(line);
            if (string.IsNullOrEmpty(log_dictionary)) return;
            lock (log_dictionary)
            {
                Directory.CreateDirectory(log_dictionary);
                string path = Path.Combine(log_dictionary, $"{now:yyyy-MM-dd}.log");
                File.AppendAllLines(path, new[] { line });
            }
        }

        internal protected bool VerifySizeLimits(Transaction tx)
        {
            if (InHigherLowPriorityList(tx)) return true;

            // Not Allow free TX bigger than MaxFreeTransactionSize
            if (tx.IsLowPriority && tx.Size > Settings.Default.MaxFreeTransactionSize) return false;

            // Require proportional fee for TX bigger than MaxFreeTransactionSize
            if (tx.Size > Settings.Default.MaxFreeTransactionSize)
            {
                Fixed8 fee = Settings.Default.FeePerExtraByte * (tx.Size - Settings.Default.MaxFreeTransactionSize);

                if (tx.NetworkFee < fee) return false;
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool InHigherLowPriorityList(Transaction tx) => Settings.Default.HighPriorityTxType.Contains(tx.Type);

        public void PreProcess(HttpContext context, string method, JArray _params)
        {
            
        }

        public JObject OnProcess(HttpContext context, string method, JArray _params)
        {
            if (method == "сheckSimplePolicyStatus")
            {
                var address = _params[0].AsString();
                bool isBlocked = Settings.Default.BlockedAccounts.List.Contains(address.ToScriptHash());
                var result = new JArray();
                var json = new JObject();
                json["blocked"] = isBlocked;
                result.Add(json);
                return result;
            }

            return null;
        }

        public void PostProcess(HttpContext context, string method, JArray _params, JObject result)
        {
            
        }
    }
}
