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

        public static Settings Default { get; }

        static Settings()
        {
            Default = new Settings(Assembly.GetExecutingAssembly().GetConfiguration());
        }

        public Settings(IConfigurationSection section)
        {
            this.MaxTransactionsPerBlock = section.GetSection("MaxTransactionsPerBlock").GetValueOrDefault(500, p => int.Parse(p));
            this.MaxFreeTransactionsPerBlock = section.GetSection("MaxFreeTransactionsPerBlock").GetValueOrDefault(20, p => int.Parse(p));
            this.BlockedAccounts = new BlockedAccounts(section.GetSection("BlockedAccounts"));
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
