using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Neo.Network.P2P.Payloads;
using Neo.Wallets;

namespace Neo.Plugins
{
    internal class Settings
    {
        public int MaxTransactionsPerBlock { get; }
        public int MaxFreeTransactionsPerBlock { get; }
        public int MaxFreeTransactionSize { get; }
        public Fixed8 FeePerExtraByte { get; }
        public BlockedAccounts BlockedAccounts { get; }
        public HashSet<TransactionType> HighPriorityTxType { get; set; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            this.MaxTransactionsPerBlock = GetValueOrDefault(section.GetSection("MaxTransactionsPerBlock"), 500, p => int.Parse(p));
            this.MaxFreeTransactionsPerBlock = GetValueOrDefault(section.GetSection("MaxFreeTransactionsPerBlock"), 20, p => int.Parse(p));
            this.MaxFreeTransactionSize = GetValueOrDefault(section.GetSection("MaxFreeTransactionSize"), 1024, p => int.Parse(p));
            this.FeePerExtraByte = GetValueOrDefault(section.GetSection("FeePerExtraByte"), Fixed8.FromDecimal(0.00001M), p => Fixed8.Parse(p));
            this.BlockedAccounts = new BlockedAccounts(section.GetSection("BlockedAccounts"));
            this.HighPriorityTxType = GetValueOrDefault(section.GetSection("HighPriorityTxType"),
                    new HashSet<TransactionType> { TransactionType.ClaimTransaction },
                    p => (TransactionType)Enum.Parse(typeof(TransactionType), p));
        }

        public HashSet<T> GetValueOrDefault<T>(IConfigurationSection section, HashSet<T> defaultValue, Func<string, T> selector)
        {
            if (section.Value == null) return defaultValue;
            return new HashSet<T>(section.GetChildren().Select(p => selector(p.Value)));
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

    internal enum PolicyType : byte
    {
        AllowAll,
        DenyAll,
        AllowList,
        DenyList
    }

    internal class BlockedAccounts
    {
        public PolicyType Type { get; }
        public HashSet<UInt160> List { get; }

        public BlockedAccounts(IConfigurationSection section)
        {
            this.Type = section.GetSection("Type").GetValueOrDefault(PolicyType.AllowAll, p => (PolicyType)Enum.Parse(typeof(PolicyType), p, true));
            this.List = new HashSet<UInt160>(section.GetSection("List").GetChildren().Select(p => p.Value.ToScriptHash()));
        }
    }
}
