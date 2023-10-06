// Copyright (C) 2023 Christopher R Schuchardt
//
// The neo-cron-plugin is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

namespace Neo.Plugins.Crontab;

public partial class CronPlugin
{
    private void OnJobFileCreated(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType != WatcherChangeTypes.Created)
            return;

        LoadJobs(e.FullPath);
    }

    private void OnJobFileDeleted(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType != WatcherChangeTypes.Deleted)
            return;

        if (_scheduler.TryGetKey(e.FullPath, out var jobEntryId))
            _ = _scheduler.TryRemove(jobEntryId, out _);
    }
}
