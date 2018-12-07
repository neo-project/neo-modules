using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Neo.Network.P2P.Payloads;

namespace Neo.Plugins
{
    internal class Settings
    {
        public int MaxTransactionsPerBlock { get; }
        public int MaxFreeTransactionsPerBlock { get; }
        public int MaxFreeTransactionSize { get; }
        public Fixed8 FeePerExtraByte { get; }
        public BlockedAccounts BlockedAccounts { get; }
        public HashSet<TransactionType> HighPriorityTx { get; set; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            MaxTransactionsPerBlock = GetValueOrDefault(section.GetSection("MaxTransactionsPerBlock"), 500, p => int.Parse(p));
            MaxFreeTransactionsPerBlock = GetValueOrDefault(section.GetSection("MaxFreeTransactionsPerBlock"), 20, p => int.Parse(p));
            MaxFreeTransactionSize = GetValueOrDefault(section.GetSection("MaxFreeTransactionSize"), 1024, p => int.Parse(p));
            FeePerExtraByte = GetValueOrDefault(section.GetSection("FeePerExtraByte"), Fixed8.FromDecimal(0.00001M), p => Fixed8.Parse(p));
            BlockedAccounts = new BlockedAccounts(section.GetSection("BlockedAccounts"));
            HighPriorityTx = new HashSet<TransactionType>(section.GetSection("HighPriorityTx").GetChildren()
                .Select(p => (TransactionType)Enum.Parse(typeof(TransactionType), p.Value)));
        }

        public T GetValueOrDefault<T>(IConfigurationSection section, T defaultValue, Func<string, T> selector)
        {
            if (section.Value == null) return defaultValue;
            return selector(section.Value);
        }

        public static void Load(IConfigurationSection section)
        {
            Default = new Settings(section);
        }
    }
}