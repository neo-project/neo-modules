// Copyright (C) 2023 Christopher R Schuchardt
//
// The neo-cron-plugin is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using NCrontab;
using Neo;
using Neo.Plugins.Crontab.Jobs;
using Neo.Plugins.Crontab.Settings;

namespace Crontab.Test.Helpers;

internal static class UT_JobHelper
{
    public static CronEntry CreateDummyJobEntry()
    {
        var settings = CreateDummySettings();
        var taskSchedule = CrontabSchedule.TryParse(settings.Expression);
        return new(taskSchedule, CronBasicJob.Create(settings), settings);
    }

    public static CronJobBasicSettings CreateDummySettings() =>
        new()
        {
            Name = "TestDummyJob",
            Expression = "* * * * *",
            Contract = new()
            {
                ScriptHash = UInt160.Zero.ToString(),
                Method = "TestMethod",
                Params = Array.Empty<CronJobContractParameterSettings>(),
            },
        };
}
