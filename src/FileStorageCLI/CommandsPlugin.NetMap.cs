using Neo.Plugins;
using Neo.ConsoleService;
using System;
using System.Security.Cryptography;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Netmap;
using System.Threading;

namespace FileStorageCLI
{
    public partial class CommandsPlugin : Plugin
    {
        [ConsoleCommand("fs epoch", Category = "FileStorageService", Description = "Get epoch")]
        private void OnGetEpoch()
        {
            if (!CheckAndParseAccount(null, out _, out ECDsa key)) return;
            using var client = new Client(key, Host);
            if (OnGetEpochInternal(client, out ulong epoch)) Console.WriteLine($"Fs current epoch:{epoch}");
        }

        [ConsoleCommand("fs localnodeinfo", Category = "FileStorageService", Description = "Get localnode info")]
        private void OnGetLocalNodeInfo()
        {
            if (!CheckAndParseAccount(null, out _, out ECDsa key)) return;
            using var client = new Client(key, Host);
            var source = new CancellationTokenSource();
            source.CancelAfter(10000);
            NodeInfo nodeInfo = client.LocalNodeInfo(context: source.Token).Result;
            source.Cancel();
            Console.WriteLine($"Fs local node info:{nodeInfo}");
        }

        private bool OnGetEpochInternal(Client client, out ulong epoch)
        {
            try
            {
                epoch = client.Epoch().Result;
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Fs get epoch fail,error:{e}");
                epoch = 0;
                return false;
            }
        }
    }
}
