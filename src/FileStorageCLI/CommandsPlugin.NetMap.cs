using System;
using System.Security.Cryptography;
using System.Threading;
using Neo.ConsoleService;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Netmap;
using Neo.Plugins;

namespace FileStorageCLI
{
    public partial class CommandsPlugin : Plugin
    {
        /// <summary>
        /// User can invoke this command to get epoch.
        /// </summary>
        [ConsoleCommand("fs epoch", Category = "FileStorageService", Description = "Get epoch")]
        private void OnGetEpoch()
        {
            if (!CheckAndParseAccount(null, out _, out ECDsa key)) return;
            using var client = OnCreateClientInternal(key);
            if (client is null) return;
            if (OnGetEpochInternal(client, out ulong epoch)) Console.WriteLine($"Fs current epoch:{epoch}");
        }

        /// <summary>
        /// User can invoke this command to get local node info.
        /// </summary>
        [ConsoleCommand("fs localnodeinfo", Category = "FileStorageService", Description = "Get localnode info")]
        private void OnGetLocalNodeInfo()
        {
            if (!CheckAndParseAccount(null, out _, out ECDsa key)) return;
            using var client = OnCreateClientInternal(key);
            if (client is null) return;
            using CancellationTokenSource source = new();
            source.CancelAfter(10000);
            try
            {
                NodeInfo nodeInfo = client.LocalNodeInfo(context: source.Token).Result;
                source.Cancel();
                Console.WriteLine($"Fs local node info:{nodeInfo}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Fs get localnode info fault,error:{e}");
                source.Cancel();
            }
        }

        //internal function
        private bool OnGetEpochInternal(Client client, out ulong epoch)
        {
            epoch = 0;
            try
            {
                epoch = client.Epoch().Result;
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Fs get epoch fail,error:{e}");
                return false;
            }
        }
    }
}
