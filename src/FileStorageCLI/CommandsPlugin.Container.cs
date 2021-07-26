using Neo.Plugins;
using Neo.ConsoleService;
using System;
using Neo;
using System.Linq;
using Neo.FileStorage.API.Cryptography;
using System.Security.Cryptography;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Container;
using Neo.FileStorage.API.Acl;
using System.Threading;
using Neo.FileStorage.API.Refs;
using System.Collections.Generic;
using Google.Protobuf;

namespace FileStorageCLI
{
    public partial class CommandsPlugin : Plugin
    {
        ///todo
        [ConsoleCommand("fs container put", Category = "FileStorageService", Description = "Create a container")]
        private void OnPutContainer(string paccount = null)
        {
            if (NoWallet()) return;
            UInt160 account = paccount is null ? currentWallet.GetAccounts().Where(p => !p.WatchOnly).ToArray()[0].ScriptHash : UInt160.Parse(paccount);
            if (!CheckAccount(account)) return;
            var host = Settings.Default.host;
            ECDsa key = currentWallet.GetAccount(account).GetKey().Export().LoadWif();
            using (var client = new Client(key, host))
            {
                var replica = new Replica(2, "");
                var policy = new PlacementPolicy(2, new Replica[] { replica }, null, null);
                var container = new Container
                {
                    Version = Neo.FileStorage.API.Refs.Version.SDKVersion(),
                    OwnerId = key.ToOwnerID(),
                    Nonce = Guid.NewGuid().ToByteString(),
                    BasicAcl = (uint)BasicAcl.PublicBasicRule,
                    PlacementPolicy = policy,
                };
                container.Attributes.Add(new Container.Types.Attribute
                {
                    Key = "CreatedAt",
                    Value = DateTime.UtcNow.ToString(),
                });
                var source = new CancellationTokenSource();
                source.CancelAfter(TimeSpan.FromMinutes(1));
                var cid = client.PutContainer(container, context: source.Token).Result;
                Console.WriteLine($"The container put request has been submitted, please confirm in the next block,ContainerId:{cid.ToBase58String()}");
            }
        }

        [ConsoleCommand("fs container delete", Category = "FileStorageService", Description = "Delete a container")]
        private void OnDeleteContainer(string containerId, string paccount = null)
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
                source.CancelAfter(10000);
                client.DeleteContainer(cid, context: source.Token).Wait();
                Console.WriteLine($"The container delete request has been submitted, please confirm in the next block,ContainerId:{containerId}");
            }
        }

        [ConsoleCommand("fs container get", Category = "FileStorageService", Description = "Get container info")]
        private void OnGetContainer(string containerId, string paccount = null)
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
                var container = client.GetContainer(cid, context: source.Token).Result;
                Console.WriteLine($"Container info:{container.Container.ToJson()}");
            }
        }

        [ConsoleCommand("fs container list", Category = "FileStorageService", Description = "List container")]
        private void OnListContainer(string paccount)
        {
            if (NoWallet()) return;
            UInt160 account = paccount is null ? currentWallet.GetAccounts().Where(p => !p.WatchOnly).ToArray()[0].ScriptHash : UInt160.Parse(paccount);
            if (!CheckAccount(account)) return;
            var host = Settings.Default.host;
            ECDsa key = currentWallet.GetAccount(account).GetKey().Export().LoadWif();
            using (var client = new Client(key, host))
            {
                var ownerID = key.ToOwnerID();
                var source = new CancellationTokenSource();
                List<ContainerID> containerLists = client.ListContainers(ownerID, context: source.Token).Result;
                Console.WriteLine($"Container list:");
                containerLists.ForEach(p => Console.WriteLine($"ContainerID:{p.ToBase58String()}"));
            }
        }
    }
}
