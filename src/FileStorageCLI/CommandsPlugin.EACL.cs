using Neo.Plugins;
using Neo.ConsoleService;
using System;
using System.Security.Cryptography;
using Neo.FileStorage.API.Client;
using System.Threading;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.Acl;

namespace FileStorageCLI
{
    public partial class CommandsPlugin : Plugin
    {
        [ConsoleCommand("fs container eacl get", Category = "FileStorageService", Description = "Get container eacl")]
        private void OnGetContainerEACL(string containerId, string paccount = null)
        {
            if (!CheckAndParseAccount(paccount, out _, out ECDsa key)) return;
            using var client = new Client(key, Host);
            var cid = ContainerID.FromBase58String(containerId);
            using var source = new CancellationTokenSource();
            source.CancelAfter(TimeSpan.FromMinutes(1));
            var eAcl = client.GetEAcl(cid, context: source.Token).Result;
            source.Cancel();
            Console.WriteLine($"Container eacl info: cid:{containerId},eacl:{eAcl.Table}");
        }

        [ConsoleCommand("fs container eacl set", Category = "FileStorageService", Description = "Set container eacl")]
        private void OnSetContainerEACL(string eaclString, string paccount = null)
        {
            if (!CheckAndParseAccount(paccount, out _, out ECDsa key)) return;
            EACLTable table = EACLTable.Parser.ParseJson(eaclString);
            using var client = new Client(key, Host);
            using var source = new CancellationTokenSource();
            source.CancelAfter(TimeSpan.FromMinutes(1));
            client.SetEACL(table, context: source.Token).Wait();
            source.Cancel();
            Console.WriteLine($"The eacl set request has been submitted,please confirm in the next block");
        }
    }
}
