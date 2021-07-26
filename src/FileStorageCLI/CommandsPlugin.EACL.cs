using Neo.Plugins;
using Neo.ConsoleService;
using System;
using Neo;
using System.Linq;
using Neo.FileStorage.API.Cryptography;
using System.Security.Cryptography;
using Neo.FileStorage.API.Client;
using System.Threading;
using Neo.FileStorage.API.Refs;

namespace FileStorageCLI
{
    public partial class CommandsPlugin : Plugin
    {
        [ConsoleCommand("fs container eacl get", Category = "FileStorageService", Description = "Get container eacl")]
        private void OnGetContainerEACL(string containerId, string paccount = null)
        {
            if (NoWallet()) return;
            UInt160 account = paccount is null ? currentWallet.GetAccounts().Where(p => !p.WatchOnly).ToArray()[0].ScriptHash : UInt160.Parse(paccount);
            if (!CheckAccount(account)) return;
            var host = Settings.Default.host;
            ECDsa key = currentWallet.GetAccount(account).GetKey().Export().LoadWif();
            using (var client = new Client(key, host))
            {
                var cid = ContainerID.FromBase58String(containerId);
                var source = new CancellationTokenSource();
                var eAcl = client.GetEAcl(cid, context: source.Token).Result;
                Console.WriteLine($"Container eacl info: cid:{containerId},eacl:{eAcl.Table}");
            }
        }
    }
}
