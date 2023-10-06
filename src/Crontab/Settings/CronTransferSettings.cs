// Copyright (C) 2023 Christopher R Schuchardt
//
// The neo-cron-plugin is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

namespace Neo.Plugins.Crontab.Settings;

public class CronTransferSettings
{
    public string AssetId { get; set; }
    public string SendTo { get; set; }
    public string SendAmount { get; set; }
    public string Comment { get; set; }
}
