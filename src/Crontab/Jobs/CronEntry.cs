// Copyright (C) 2023 Christopher R Schuchardt
//
// The neo-cron-plugin is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using NCrontab;
using Neo.Plugins.Crontab.Settings;

namespace Neo.Plugins.Crontab.Jobs;

internal class CronEntry
{
    public ICronJob Job { get; }
    public ICronJobSettings Settings { get; }
    public CrontabSchedule Schedule { get; }
    public bool IsEnabled { get; internal set; }

    internal CronEntry(
        CrontabSchedule schedule,
        ICronJob job,
        ICronJobSettings settings)
    {
        Schedule = schedule;
        Job = job;
        Settings = settings;
        IsEnabled = true;
    }
}
