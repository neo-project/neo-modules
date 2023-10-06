// Copyright (C) 2023 Christopher R Schuchardt
//
// The neo-cron-plugin is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

namespace Neo.Plugins.Crontab.Settings;

public class CronJobWalletSettings
{
    public string Path { get; set; }
    public string Password { get; set; }
    public string Account { get; set; }
    public string[] Signers { get; set; }
}
