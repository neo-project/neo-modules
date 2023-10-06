// Copyright (C) 2023 Christopher R Schuchardt
//
// The neo-cron-plugin is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using Neo.Plugins.Crontab.Jobs;
using Neo.Plugins.Crontab.Settings;

namespace Neo.Plugins.Crontab;

public partial class CronPlugin : Plugin
{
    public override string Name => "Crontab";
    public override string Description => "Task scheduler for sending transactions to the blockchain.";

    internal static NeoSystem NeoSystem { get; private set; }

    private readonly CronScheduler _scheduler;

    public CronPlugin()
    {
        _scheduler = new();
    }

    public override void Dispose()
    {
        _scheduler.Dispose();
        GC.SuppressFinalize(this);
    }

    protected override void Configure() =>
        CronPluginSettings.Load(GetConfiguration());

    protected override void OnSystemLoaded(NeoSystem system)
    {
        if (system.Settings.Network != CronPluginSettings.Current.Network)
            return;
        NeoSystem = system;
        SearchForJobs();
        StartFileWatcher();
    }
}
