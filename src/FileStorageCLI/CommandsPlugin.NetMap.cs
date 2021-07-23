using Neo.Plugins;
using Neo.ConsoleService;
using System;
using Neo;
using System.Linq;
using Neo.FileStorage.API.Cryptography;
using System.Security.Cryptography;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Netmap;
using System.Threading;

namespace FileStorageCLI
{
    public partial class CommandsPlugin : Plugin
    {
        [ConsoleCommand("fs epoch", Category = "FileStorageService", Description = "Get epoch")]
        private void OnGetEpoch(string paccount = null)
        {
            if (NoWallet()) return;
            UInt160 account = paccount is null ? currentWallet.GetAccounts().ToArray()[0].ScriptHash : UInt160.Parse(paccount);
            if (!CheckAccount(account)) return;
            var host = Settings.Default.host;
            ECDsa key = currentWallet.GetAccount(account).GetKey().Export().LoadWif();
            using (var client = new Client(key, host))
            {
                var source = new CancellationTokenSource();
                source.CancelAfter(10000);
                ulong epoch = client.Epoch(context: source.Token).Result;
                Console.WriteLine($"Fs current epoch:{epoch}");
            }
        }

        [ConsoleCommand("fs localnodeinfo", Category = "FileStorageService", Description = "Get localnode info")]
        private void OnGetLocalNodeInfo(string paccount = null)
        {
            if (NoWallet()) return;
            UInt160 account = paccount is null ? currentWallet.GetAccounts().ToArray()[0].ScriptHash : UInt160.Parse(paccount);
            if (!CheckAccount(account)) return;
            var host = Settings.Default.host;
            ECDsa key = currentWallet.GetAccount(account).GetKey().Export().LoadWif();
            using (var client = new Client(key, host))
            {
                var source = new CancellationTokenSource();
                source.CancelAfter(10000);
                NodeInfo nodeInfo = client.LocalNodeInfo(context: source.Token).Result;
                Console.WriteLine($"Fs local node info:{nodeInfo}");
            }
        }
    }
}
