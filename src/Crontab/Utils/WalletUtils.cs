// Copyright (C) 2023 Christopher R Schuchardt
//
// The neo-cron-plugin is free software distributed under the
// MIT software license, see the accompanying file LICENSE in
// the main directory of the project for more details.

using Akka.Actor;
using Neo.ConsoleService;
using Neo.Network.P2P.Payloads;
using Neo.Plugins.Crontab.Jobs;
using Neo.Plugins.Crontab.Settings;
using Neo.SmartContract;
using Neo.Wallets;

namespace Neo.Plugins.Crontab.Utils;

internal static class WalletUtils
{
    public static void MakeTransferAndSendTx(CronTransferJob transferJob)
    {
        var asset = new AssetDescriptor(CronPlugin.NeoSystem.StoreView, CronPlugin.NeoSystem.Settings, transferJob.TokenHash);
        var amount = new BigDecimal(transferJob.SendAmount, asset.Decimals);

        try
        {
            var tx = transferJob.Wallet.MakeTransaction(CronPlugin.NeoSystem.StoreView, new[]
            {
                new TransferOutput()
                {
                    AssetId = transferJob.TokenHash,
                    Value = amount,
                    ScriptHash = transferJob.SendTo,
                    Data = transferJob.Comment,
                }
            }, transferJob.Sender, transferJob.Signers);
            SignAndSendTx(transferJob.Wallet, tx);
        }
        catch (Exception ex)
        {
            ConsoleHelper.Error($"Cron:Job[\"{transferJob.Name}\"]::\"{ex.Message}\"");
        }

    }

    public static void MakeInvokeAndSendTx(CronBasicJob basicJob)
    {
        if (basicJob != null || basicJob.Wallet != null && basicJob.Sender != null)
            try
            {
                if (ContractUtils.BuildInvokeMethod(basicJob.Contract, out var script) == false)
                    ConsoleHelper.Error($"Cron:Job[\"{basicJob.Name}\"]::\"Can not find method {basicJob.Contract.Method} with parameter count {basicJob.Contract.Params.Length}.\"");
                else
                {
                    var tx = basicJob.Wallet.MakeTransaction(
                        CronPlugin.NeoSystem.StoreView,
                        script,
                        basicJob.Sender,
                        basicJob.Signers,
                        maxGas: CronPluginSettings.Current.MaxGasInvoke);

                    SignAndSendTx(basicJob.Wallet, tx);
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.Error($"Cron:Job[\"{basicJob.Name}\"]::\"{ex.Message}\"");
            }
    }

    public static void SignAndSendTx(Wallet wallet, Transaction tx)
    {
        var context = new ContractParametersContext(CronPlugin.NeoSystem.StoreView, tx, CronPlugin.NeoSystem.Settings.Network);
        if (wallet.Sign(context) && context.Completed)
        {
            tx.Witnesses = context.GetWitnesses();
            CronPlugin.NeoSystem.Blockchain.Tell(tx);
        }
    }
}
