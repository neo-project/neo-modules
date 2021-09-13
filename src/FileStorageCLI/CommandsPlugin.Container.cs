using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using Google.Protobuf;
using Neo;
using Neo.ConsoleService;
using Neo.FileStorage.API.Container;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Refs;
using static Neo.FileStorage.API.Policy.Helper;
using Neo.Plugins;
using System.Linq;

namespace FileStorageCLI
{
    public partial class CommandsPlugin : Plugin
    {
        /// <summary>
        /// User can invoke this command to create container in Fs.
        /// </summary>
        /// <param name="policyString">policy</param>
        /// <param name="basicAcl">basic-acl</param>
        /// <param name="attributesString">attribute,format:< <key1> - <value1> _ <key2> - <value2> ></param>
        /// <param name="paddress">account address(The first account of the wallet,default)</param>
        [ConsoleCommand("fs container put", Category = "FileStorageService", Description = "Create a container")]
        private void OnPutContainer(string policyString, string basicAcl, string attributesString, string paddress = null)
        {
            if (!CheckAndParseAccount(paddress, out _, out ECDsa key)) return;
            using var client = OnCreateClientInternal(key);
            if (client is null) return;
            var policy = ParsePlacementPolicy(policyString);
            var container = new Container
            {
                Version = Neo.FileStorage.API.Refs.Version.SDKVersion(),
                OwnerId = OwnerID.FromScriptHash(key.PublicKey().PublicKeyToScriptHash()),
                Nonce = Guid.NewGuid().ToByteString(),
                BasicAcl = uint.Parse(basicAcl),
                PlacementPolicy = policy,
            };
            Container.Types.Attribute[] attributes = attributesString.Split("_").Select(p => new Container.Types.Attribute() { Key = p.Split("-")[0], Value = p.Split("-")[1] }).ToArray();
            container.Attributes.Add(attributes);
            var source = new CancellationTokenSource();
            source.CancelAfter(TimeSpan.FromMinutes(1));
            try
            {
                var cid = client.PutContainer(container, context: source.Token).Result;
                source.Cancel();
                Console.WriteLine($"The container put request has been submitted, please confirm in the next block,ContainerId:{cid.String()}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Fs put container fault, error:{e}");
                source.Cancel();
            }
        }

        /// <summary>
        /// User can invoke this command to delete container.
        /// </summary>
        /// <param name="containerId">containerId</param>
        /// <param name="paddress">account address(The first account of the wallet,default)</param>
        [ConsoleCommand("fs container delete", Category = "FileStorageService", Description = "Delete a container")]
        private void OnDeleteContainer(string containerId, string paddress = null)
        {
            if (!CheckAndParseAccount(paddress, out _, out ECDsa key)) return;
            using var client = OnCreateClientInternal(key);
            if (client is null) return;
            using var source = new CancellationTokenSource();
            source.CancelAfter(10000);
            if (!ParseContainerID(containerId, out var cid)) return;
            try
            {
                client.DeleteContainer(cid, context: source.Token).Wait();
                Console.WriteLine($"The container delete request has been submitted, please confirm in the next block,ContainerId:{containerId}");
                source.Cancel();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Fs delete container fault,error:{e}");
                source.Cancel();
            }
        }

        /// <summary>
        /// User can invoke this command to query container info.
        /// </summary>
        /// <param name="containerId">containerId</param>
        /// <param name="paddress">account address(The first account of the wallet,default)</param>
        [ConsoleCommand("fs container get", Category = "FileStorageService", Description = "Get container info")]
        private void OnGetContainer(string containerId, string paddress = null)
        {
            if (!CheckAndParseAccount(paddress, out _, out ECDsa key)) return;
            using var client = OnCreateClientInternal(key);
            if (client is null) return;
            if (!ParseContainerID(containerId, out var cid)) return;
            using var source = new CancellationTokenSource();
            source.CancelAfter(10000);
            try
            {
                var container = client.GetContainer(cid, context: source.Token).Result;
                source.Cancel();
                Console.WriteLine($"Container info:{container.Container.ToJson()}");
            }
            catch (Exception e)
            {
                source.Cancel();
                Console.WriteLine($"Fs get container fault,error:{e}");
            }
        }

        /// <summary>
        /// User can invoke this command to get all containerids belong to account.
        /// </summary>
        /// <param name="paddress">account address</param>
        [ConsoleCommand("fs container list", Category = "FileStorageService", Description = "List container")]
        private void OnListContainer(string paddress)
        {
            if (!CheckAndParseAccount(paddress, out UInt160 account, out ECDsa key)) return;
            using var client = OnCreateClientInternal(key);
            if (client is null) return;
            using var source = new CancellationTokenSource();
            source.CancelAfter(10000);
            OwnerID ownerID = OwnerID.FromScriptHash(key.PublicKey().PublicKeyToScriptHash());
            try
            {
                List<ContainerID> containerLists = client.ListContainers(ownerID, context: source.Token).Result;
                source.Cancel();
                Console.WriteLine($"Container list:");
                containerLists.ForEach(p => Console.WriteLine($"ContainerID:{p.String()}"));
            }
            catch (Exception e)
            {
                source.Cancel();
                Console.WriteLine($"Fs get container list fault,error:{e}");
            }
        }

        // internal function
        private bool ParseContainerID(string containerId, out ContainerID cid)
        {
            cid = null;
            try
            {
                cid = ContainerID.FromString(containerId);
                return true;
            }
            catch
            {
                Console.WriteLine($"Parse ContainerId falut");
                return false;
            }
        }

        private bool ParseObjectID(string objectId, out ObjectID oid)
        {
            oid = null;
            try
            {
                oid = ObjectID.FromString(objectId);
                return true;
            }
            catch
            {
                Console.WriteLine($"Parse ObjectId falut");
                return false;
            }
        }
    }
}
