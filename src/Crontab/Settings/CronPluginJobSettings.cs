// Copyright (C) 2023 Christopher R Schuchardt
//
// The Neo.Plugins.Crontab is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

namespace Neo.Plugins.Crontab.Settings;

public class CronPluginJobSettings
{
    public string Path { get; set; }
    public uint Timeout { get; set; }
}
