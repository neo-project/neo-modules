using Microsoft.Extensions.Configuration;
using System;

namespace Neo.Plugins
{
    internal class Settings
    {
        /// <summary>
        /// Probability of rejecting consensus messages - by type
        /// </summary>
        public double ProbRejectPrepRequest { get; }
        public double ProbRejectPrepResponse { get; }
        public double ProbRejectCommit { get; }
        public double ProbRejectChangeView { get; }
        public double ProbRejectRecover { get; }

        public static Settings Default { get; private set; }

        private Settings(IConfigurationSection section)
        {
            this.ProbRejectPrepRequest = GetValueOrDefault(section.GetSection("ProbRejectPrepRequest"), 0.25, p => double.Parse(p));
            this.ProbRejectPrepResponse = GetValueOrDefault(section.GetSection("ProbRejectPrepResponse"), 0.5, p => double.Parse(p));
            this.ProbRejectCommit = GetValueOrDefault(section.GetSection("ProbRejectCommit"), 0.5, p => double.Parse(p));
            this.ProbRejectChangeView = GetValueOrDefault(section.GetSection("ProbRejectChangeView"), 0, p => double.Parse(p));
            this.ProbRejectRecover = GetValueOrDefault(section.GetSection("ProbRejectRecover"), 0, p => double.Parse(p));
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
