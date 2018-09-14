using Microsoft.Extensions.Configuration;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Neo.Plugins
{
    internal class Settings
    {
        public int MaxTransactionsPerBlock { get; }
        public int MaxFreeTransactionsPerBlock { get; }
        public BlockedAccounts BlockedAccounts { get; }
        public int MaxTransactionSize { get; }
        public int MaxFreeTransactionSize { get; }
        public int TransactionExtraSize { get; }
        public decimal FeePerExtraByte { get; }

        public static Settings Default { get; }

        static Settings()
        {
            Default = new Settings(Assembly.GetExecutingAssembly().GetConfiguration());
        }

        public Settings(IConfigurationSection section)
        {
            this.MaxTransactionsPerBlock = GetValueOrDefault(section.GetSection("MaxTransactionsPerBlock"), 500, p => int.Parse(p));
            this.MaxFreeTransactionsPerBlock = GetValueOrDefault(section.GetSection("MaxFreeTransactionsPerBlock"), 20, p => int.Parse(p));
            this.BlockedAccounts = new BlockedAccounts(section.GetSection("BlockedAccounts"));
            this.MaxTransactionSize = GetValueOrDefault(section.GetSection("MaxTransactionSize"), 100000, p => int.Parse(p));
            this.MaxFreeTransactionSize = GetValueOrDefault(section.GetSection("MaxFreeTransactionSize"), 300, p => int.Parse(p));
            this.TransactionExtraSize = GetValueOrDefault(section.GetSection("TransactionExtraSize"), 2000, p => int.Parse(p));
            this.FeePerExtraByte = GetValueOrDefault(section.GetSection("FeePerExtraByte"), 0.00001M, p => decimal.Parse(p));
        }

        public T GetValueOrDefault<T>(IConfigurationSection section, T defaultValue, Func<string, T> selector)
        {
            if (section.Value == null) return defaultValue;
            return selector(section.Value);
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
            this.Type = (PolicyType)Enum.Parse(typeof(PolicyType), section.GetSection("Type").Value, true);
            this.List = new HashSet<UInt160>(section.GetSection("List").GetChildren().Select(p => p.Value.ToScriptHash()));
        }
    }
}
