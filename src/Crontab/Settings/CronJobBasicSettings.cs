// Copyright (C) 2023 Christopher R Schuchardt
//
// The neo-cron-plugin is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using Microsoft.Extensions.Configuration;

namespace Neo.Plugins.Crontab.Settings;

public class CronJobBasicSettings : ICronJobSettings
{
    public string Filename { get; set; }
    public string Name { get; set; }
    public string Expression { get; set; }
    public bool RunOnce { get; set; }
    public CronJobContractSettings Contract { get; set; }
    public CronJobWalletSettings Wallet { get; set; }

    public static CronJobBasicSettings Load(IConfiguration config, string filename) =>
        new()
        {
            Filename = filename,
            Name = config.GetValue<string>(nameof(Name)),
            Expression = config.GetValue<string>(nameof(Expression)),
            RunOnce = config.GetValue<bool>(nameof(RunOnce)),
            Contract = config.GetSection(nameof(Contract)).Get<CronJobContractSettings>(),
            Wallet = config.GetSection(nameof(Wallet)).Get<CronJobWalletSettings>(),
        };
}
