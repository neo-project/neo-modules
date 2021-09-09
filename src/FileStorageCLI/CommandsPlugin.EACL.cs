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
            if (!ParseContainerID(containerId, out ContainerID cid)) return;
            using var client = OnCreateClientInternal(key);
            if (client is null) return;
            using var source = new CancellationTokenSource();
            source.CancelAfter(TimeSpan.FromMinutes(1));
            try {
                var eAcl = client.GetEAcl(cid, context: source.Token).Result;
                source.Cancel();
                Console.WriteLine($"Eacl Info: cid:{containerId},eacl:{eAcl.Table}");
            } catch (Exception e) {
                source.Cancel();
                Console.WriteLine($"Fs get eacl fault,error:{e}");
            }
        }

        [ConsoleCommand("fs container eacl set", Category = "FileStorageService", Description = "Set container eacl")]
        private void OnSetContainerEACL(string eaclString, string paccount = null)
        {
            if (!CheckAndParseAccount(paccount, out _, out ECDsa key)) return;
            EACLTable table = EACLTable.Parser.ParseJson(eaclString);
            using var client = OnCreateClientInternal(key);
            if (client is null) return;
            using var source = new CancellationTokenSource();
            source.CancelAfter(TimeSpan.FromMinutes(1));
            try {
                client.SetEACL(table, context: source.Token).Wait();
                source.Cancel();
                Console.WriteLine($"The eacl set request has been submitted,please confirm in the next block");
            } catch (Exception e) {
                source.Cancel();
                Console.WriteLine($"Fs set eacl fault,error:{e}");
            }
        }
    }
}
