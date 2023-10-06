// Copyright (C) 2023 Christopher R Schuchardt
//
// The neo-cron-plugin is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using Neo.ConsoleService;
using Neo.Plugins.Crontab.Settings;

namespace Neo.Plugins.Crontab;

public partial class CronPlugin
{
    [ConsoleCommand("crontab disable", Category = "Crontab Commands", Description = "Disables a crontab job from running.")]
    private void OnDisableTask(string jobId)
    {
        _ = Guid.TryParse(jobId, out var id);
        if (_scheduler.Entries.TryGetValue(id, out var jobEntry) == false)
            ConsoleHelper.Error($"Could not find the crontab job with id {jobId:n}.");
        else
        {
            jobEntry.IsEnabled = false;
            ConsoleHelper.Info("", $"Disabled ", $"{id:n}", " successfully.");
        }
    }

    [ConsoleCommand("crontab enable", Category = "Crontab Commands", Description = "Enables a crontab job to run its schedule.")]
    private void OnEnableTask(string jobId)
    {
        _ = Guid.TryParse(jobId, out var id);
        if (_scheduler.Entries.TryGetValue(id, out var jobEntry) == false)
            ConsoleHelper.Error($"Could not find the crontab job with id {jobId:n}.");
        else
        {
            jobEntry.IsEnabled = true;
            ConsoleHelper.Info("", $"Enabled ", $"{id:n}", " successfully.");
        }
    }

    [ConsoleCommand("crontab list", Category = "Crontab Commands", Description = "List all the crontab jobs.")]
    private void OnListCrontabJobs()
    {
        if (_scheduler.Entries.Any() == true)
            ConsoleHelper.Info("--------------------------------------------");

        foreach (var entry in _scheduler.Entries)
        {
            ConsoleHelper.Info("        ID: ", $"{entry.Key:n}");
            ConsoleHelper.Info("      File: ", $"{entry.Value.Settings.Filename}");
            ConsoleHelper.Info("      Name: ", $"{entry.Value.Settings.Name}");
            ConsoleHelper.Info("  Schedule: ", $"{entry.Value.Settings.Expression}");
            ConsoleHelper.Info("   Enabled: ", $"{entry.Value.IsEnabled}");
            ConsoleHelper.Info("  Run Once: ", $"{entry.Value.Settings.RunOnce}");
            if (entry.Value.Job.NextRunTimestamp != default)
                ConsoleHelper.Info("  Next Run: ", $"{entry.Value.Job.NextRunTimestamp:MM/dd/yyyy hh:mm tt}");
            else
                ConsoleHelper.Info("  Next Run: ", $"N/A");
            if (entry.Value.Job.LastRunTimestamp != default)
                ConsoleHelper.Info("  Last Run: ", $"{entry.Value.Job.LastRunTimestamp.ToLocalTime():MM/dd/yyyy hh:mm tt}");
            else
                ConsoleHelper.Info("  Last Run: ", $"N/A");

            if (entry.Value.Settings.GetType() == typeof(CronJobBasicSettings))
            {
                var contractSettings = entry.Value.Settings as CronJobBasicSettings;
                ConsoleHelper.Info("", "-------", "Contract", "-------");
                ConsoleHelper.Info("ScriptHash: ", $"{contractSettings.Contract.ScriptHash}");
                ConsoleHelper.Info("    Method: ", $"{contractSettings.Contract.Method}");
                ConsoleHelper.Info("Parameters: ", $"[{string.Join(", ", contractSettings.Contract.Params.Select(s => $"\"{s.Value}\""))}]");
            }
            else if (entry.Value.Settings.GetType() == typeof(CronJobTransferSettings))
            {
                var transferSettings = entry.Value.Settings as CronJobTransferSettings;
                ConsoleHelper.Info("", "-------", "Transfer", "-------");
                ConsoleHelper.Info("  Asset Id: ", $"{transferSettings.Transfer.AssetId}");
                ConsoleHelper.Info("   Send To: ", $"{transferSettings.Transfer.SendTo}");
                ConsoleHelper.Info("    Amount: ", $"{transferSettings.Transfer.SendAmount}");
                ConsoleHelper.Info("   Comment: ", $"{transferSettings.Transfer.Comment}");
            }
            ConsoleHelper.Info("", "--------", "Wallet", "--------");
            ConsoleHelper.Info("      Path: ", $"{entry.Value.Settings.Wallet.Path}");
            ConsoleHelper.Info("   Account: ", $"{entry.Value.Settings.Wallet.Account}");
            ConsoleHelper.Info("   Signers: ", $"[{string.Join(", ", entry.Value.Job.Signers.Select(s => $"\"{s.Account}\""))}]");

            if (_scheduler.Entries.Count > 1)
                ConsoleHelper.Info("--------------------------------------------");
        }

        if (_scheduler.Entries.Count == 1)
            ConsoleHelper.Info("--------------------------------------------");

        ConsoleHelper.Info("", "Total: ", $"{_scheduler.Entries.Count}", " job(s).");
    }
}
