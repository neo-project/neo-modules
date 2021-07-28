using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using Google.Protobuf;
using Neo;
using Neo.ConsoleService;
using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Container;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.Plugins;

namespace FileStorageCLI
{
    public partial class CommandsPlugin : Plugin
    {
        ///todo
        [ConsoleCommand("fs container put", Category = "FileStorageService", Description = "Create a container")]
        private void OnPutContainer(string paccount = null)
        {
            if (!CheckAndParseAccount(paccount, out _, out ECDsa key, out _, out _)) return;
            using var client = new Client(key, Host);
            var replica = new Replica(2, "");
            var policy = new PlacementPolicy(2, new Replica[] { replica }, null, null);
            var container = new Container
            {
                Version = Neo.FileStorage.API.Refs.Version.SDKVersion(),
                OwnerId = OwnerID.FromScriptHash(key.PublicKey().PublicKeyToScriptHash()),
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

        [ConsoleCommand("fs container delete", Category = "FileStorageService", Description = "Delete a container")]
        private void OnDeleteContainer(string containerId, string paccount = null)
        {
            if (!CheckAndParseAccount(paccount, out _, out ECDsa key, out _, out _)) return;
            using var client = new Client(key, Host);
            using var source = new CancellationTokenSource();
            source.CancelAfter(10000);
            var cid = ContainerID.FromBase58String(containerId);
            client.DeleteContainer(cid, context: source.Token).Wait();
            Console.WriteLine($"The container delete request has been submitted, please confirm in the next block,ContainerId:{containerId}");
        }

        [ConsoleCommand("fs container get", Category = "FileStorageService", Description = "Get container info")]
        private void OnGetContainer(string containerId, string paccount = null)
        {
            if (!CheckAndParseAccount(paccount, out _, out ECDsa key, out _, out _)) return;
            using var client = new Client(key, Host);
            var cid = ContainerID.FromBase58String(containerId);
            using var source = new CancellationTokenSource();
            var container = client.GetContainer(cid, context: source.Token).Result;
            source.Cancel();
            Console.WriteLine($"Container info:{container.Container.ToJson()}");
        }

        [ConsoleCommand("fs container list", Category = "FileStorageService", Description = "List container")]
        private void OnListContainer(string paccount)
        {
            if (!CheckAndParseAccount(paccount, out UInt160 account, out ECDsa key, out Neo.Cryptography.ECC.ECPoint pk, out OwnerID ownerID)) return;
            using var client = new Client(key, Host);
            using var source = new CancellationTokenSource();
            List<ContainerID> containerLists = client.ListContainers(ownerID, context: source.Token).Result;
            source.Cancel();
            Console.WriteLine($"Container list:");
            containerLists.ForEach(p => Console.WriteLine($"ContainerID:{p.ToBase58String()}"));
        }
    }
}
