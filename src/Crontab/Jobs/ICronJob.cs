// Copyright (C) 2023 Christopher R Schuchardt
//
// The neo-cron-plugin is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using Neo.Network.P2P.Payloads;
using Neo.Wallets;

namespace Neo.Plugins.Crontab.Jobs;

internal interface ICronJob
{
    CronJobType Type { get; }
    string Name { get; }
    string Expression { get; }
    Wallet Wallet { get; }
    UInt160 Sender { get; }
    Signer[] Signers { get; }
    DateTime LastRunTimestamp { get; }
    DateTime NextRunTimestamp { get; set; }
    void Run(DateTime timerNow);
}
