// Copyright (C) 2023 Christopher R Schuchardt
//
// The neo-cron-plugin is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using Crontab.Test.Helpers;
using Neo.Plugins.Crontab.Jobs;

namespace Crontab.Test;

public class UT_CronScheduler
{
    private readonly CronScheduler _cronScheduler;

    public UT_CronScheduler()
    {
        _cronScheduler = new();
    }

    [Fact]
    public void Test_Job_Entries()
    {
        Test_TryAdd();

        Thread.Sleep(3000);

        Test_TryRemove();
    }

    private void Test_TryAdd()
    {
        var dummyJobEntry = UT_JobHelper.CreateDummyJobEntry();

        var result = _cronScheduler.TryAdd(dummyJobEntry, out var jobEntryId);

        Assert.True(result);
        Assert.NotEqual(Guid.Empty, jobEntryId);

        var jobItem = new KeyValuePair<Guid, CronEntry>(jobEntryId, dummyJobEntry);
        Assert.Contains(jobItem, _cronScheduler.Entries);
    }

    private void Test_TryRemove()
    {
        var jobEntryKvp = _cronScheduler.Entries.FirstOrDefault();

        Assert.NotEqual(Guid.Empty, jobEntryKvp.Key);
        Assert.NotNull(jobEntryKvp.Value);

        var doesExists = _cronScheduler.ContainsTask(jobEntryKvp.Value.Job);

        Assert.True(doesExists);

        var isRemoved = _cronScheduler.TryRemove(jobEntryKvp.Key, out var jobEntry);

        Assert.True(isRemoved);
        Assert.NotNull(jobEntry);

        doesExists = _cronScheduler.ContainsTask(jobEntry.Job);

        Assert.False(doesExists);
    }
}
