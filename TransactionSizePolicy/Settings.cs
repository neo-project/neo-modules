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
        public int MaxTransactionSize { get; }
        public int MaxFreeTransactionSize { get; }
        public int TransactionExtraSize { get; }
        public int ExtraSizeChunk { get; }
        public decimal ExtraChunkFee { get; }

        public static Settings Default { get; }

        static Settings()
        {
            Default = new Settings(Assembly.GetExecutingAssembly().GetConfiguration());
        }

        public Settings(IConfigurationSection section)
        {
            MaxTransactionSize = GetValueOrDefault(section.GetSection("MaxTransactionSize"), 100000, p => int.Parse(p));
            MaxFreeTransactionSize = GetValueOrDefault(section.GetSection("MaxFreeTransactionSize"), 300, p => int.Parse(p));

            TransactionExtraSize = GetValueOrDefault(section.GetSection("TransactionExtraSize"), 2000, p => int.Parse(p));
            ExtraSizeChunk = GetValueOrDefault(section.GetSection("ExtraSizeChunk"), 100, p => int.Parse(p));
            ExtraChunkFee = GetValueOrDefault(section.GetSection("ExtraChunkFee"), 0.001M, p => decimal.Parse(p));
        }

        public T GetValueOrDefault<T>(IConfigurationSection section, T defaultValue, Func<string, T> selector)
        {
            if (section.Value == null) return defaultValue;
            return selector(section.Value);
        }
    }
}