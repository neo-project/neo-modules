using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Neo.Wallets;

namespace Neo.Plugins
{
    internal class BlockedAccounts
    {
        public PolicyType Type { get; }
        public HashSet<UInt160> List { get; }

        public BlockedAccounts(IConfigurationSection section)
        {
            Type = section.GetSection("Type").GetValueOrDefault(PolicyType.AllowAll, p => (PolicyType)Enum.Parse(typeof(PolicyType), p, true));
            List = new HashSet<UInt160>(section.GetSection("List").GetChildren().Select(p => p.Value.ToScriptHash()));
        }
    }
}