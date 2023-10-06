// Copyright (C) 2023 Christopher R Schuchardt
//
// The neo-cron-plugin is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using Microsoft.Extensions.Configuration;
using NCrontab;
using Neo.ConsoleService;
using Neo.Plugins.Crontab.Jobs;
using Neo.Plugins.Crontab.Settings;

namespace Neo.Plugins.Crontab;

public partial class CronPlugin
{
    private FileSystemWatcher _fileSystemWatcher;

    private void StartFileWatcher()
    {
        _fileSystemWatcher = new FileSystemWatcher(CronPluginSettings.Current.Job.Path)
        {
            Filter = "*.job",
            IncludeSubdirectories = true,
            EnableRaisingEvents = true,
        };

        _fileSystemWatcher.Created += OnJobFileCreated;
        _fileSystemWatcher.Deleted += OnJobFileDeleted;
    }

    private void SearchForJobs()
    {
        if (Directory.Exists(CronPluginSettings.Current.Job.Path) == false)
            return;
        foreach (var filename in Directory.EnumerateFiles(CronPluginSettings.Current.Job.Path, "*.job", SearchOption.AllDirectories))
            LoadJobs(filename);
    }

    private void LoadJobs(string filename)
    {
        try
        {
            var jobConfigRoot = new ConfigurationBuilder()
                    .AddJsonFile(filename, false, false)
                    .Build();
            switch (jobConfigRoot.GetValue(nameof(ICronJob.Type), CronJobType.Basic))
            {
                case CronJobType.Basic:
                    CreateBasicJob(CronJobBasicSettings.Load(jobConfigRoot, filename));
                    break;
                case CronJobType.Transfer:
                    CreateTransferJob(CronJobTransferSettings.Load(jobConfigRoot, filename));
                    break;
                default:
                    break;
            }
        }
        catch (InvalidDataException)
        {
            ConsoleHelper.Error($"Cron:Job:InvalidDataFormat::\"{filename}\"");
        }
    }

    private void CreateTransferJob(CronJobTransferSettings settings)
    {
        try
        {
            if (File.Exists(settings.Wallet.Path) == false)
                ConsoleHelper.Error($"Cron:Job[\"{settings.Name}\"]::\"{settings.Wallet.Path} does not exist.\"");
            var cTask = CronTransferJob.Create(settings);
            if (cTask.Wallet == null)
                ConsoleHelper.Error($"Cron:Job[\"{settings.Name}\"]::\"Invalid password.\"");
            else
                CreateJobEntry(settings, cTask);
        }
        catch (FormatException)
        {
            ConsoleHelper.Error($"Cron:Job:[\"{settings.Name}\"]::\"Invalid address format.\"");
        }
    }

    private void CreateBasicJob(CronJobBasicSettings settings)
    {
        try
        {
            if (File.Exists(settings.Wallet.Path) == false)
                ConsoleHelper.Error($"Cron:Job[\"{settings.Name}\"]::\"{settings.Wallet.Path} does not exist.\"");
            if (string.IsNullOrEmpty(settings.Contract?.Method))
                ConsoleHelper.Error($"Cron:Job[\"{settings.Name}\"]::\"Method name is invalid.\"");
            else
            {
                settings.Contract.Method = settings.Contract.Method.Length > 1 ?
                    settings.Contract.Method[0].ToString().ToLowerInvariant() + settings.Contract.Method[1..] :
                    settings.Contract.Method[0].ToString().ToLowerInvariant();
                var cTask = CronBasicJob.Create(settings);
                if (cTask.Wallet == null)
                    ConsoleHelper.Error($"Cron:Job[\"{settings.Name}\"]::\"Invalid password.\"");
                else
                    CreateJobEntry(settings, cTask);
            }
        }
        catch (FormatException)
        {
            ConsoleHelper.Error($"Cron:Job:[\"{settings.Name}\"]::\"Invalid address format.\"");
        }
    }

    private void CreateJobEntry(ICronJobSettings settings, ICronJob job)
    {
        var taskSchedule = CrontabSchedule.TryParse(settings.Expression);
        if (taskSchedule != null)
            _ = _scheduler.TryAdd(new CronEntry(taskSchedule, job, settings), out _);
        else
            ConsoleHelper.Error($"Cron:Job:[\"{settings.Name}\"]::\"Expression is invalid.\"");
    }
}
