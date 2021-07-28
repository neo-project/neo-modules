using Neo.Plugins;
using Neo.ConsoleService;
using System;
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
            if (!CheckAndParseAccount(paccount, out _, out ECDsa key, out _, out _)) return;
            using var client = new Client(key, Host);
            var cid = ContainerID.FromBase58String(containerId);
            using var source = new CancellationTokenSource();
            var eAcl = client.GetEAcl(cid, context: source.Token).Result;
            source.Cancel();
            Console.WriteLine($"Container eacl info: cid:{containerId},eacl:{eAcl.Table}");
        }
    }
}
