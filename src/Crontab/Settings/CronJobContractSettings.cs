// Copyright (C) 2023 Christopher R Schuchardt
//
// The neo-cron-plugin is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

namespace Neo.Plugins.Crontab.Settings;

public class CronJobContractSettings
{
    public string ScriptHash { get; set; }
    public string Method { get; set; }
    public CronJobContractParameterSettings[] Params { get; set; } = Array.Empty<CronJobContractParameterSettings>();
}
