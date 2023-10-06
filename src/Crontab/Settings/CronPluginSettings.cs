// Copyright (C) 2023 Christopher R Schuchardt
//
// The neo-cron-plugin is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using Microsoft.Extensions.Configuration;

namespace Neo.Plugins.Crontab.Settings;

public class CronPluginSettings
{
    public uint Network { get; private init; }
    public long MaxGasInvoke { get; private init; }
    public CronPluginJobSettings Job { get; private init; }

    public static CronPluginSettings Current { get; private set; }

    public static CronPluginSettings Default =>
        new()
        {
            Network = 860833102u,
            MaxGasInvoke = 20000000,
            Job = new()
            {
                Path = "jobs",
                Timeout = 15u,
            }
        };

    public static void Load(IConfigurationSection section) =>
        Current = new()
        {
            Network = section.GetValue(nameof(Network), Default.Network),
            MaxGasInvoke = section.GetValue(nameof(MaxGasInvoke), Default.MaxGasInvoke),
            Job = section.GetSection(nameof(Job)).Get<CronPluginJobSettings>(),
        };
}
