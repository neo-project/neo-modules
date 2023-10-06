// Copyright (C) 2023 Christopher R Schuchardt
//
// The neo-cron-plugin is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using Neo.SmartContract;

namespace Neo.Plugins.Crontab.Settings;

public class CronJobContractParameterSettings
{
    public ContractParameterType Type { get; set; }
    public string Value { get; set; }
}
