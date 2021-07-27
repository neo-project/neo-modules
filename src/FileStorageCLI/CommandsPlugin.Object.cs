using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Google.Protobuf;
using Neo;
using Neo.ConsoleService;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Cryptography.Tz;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.StorageGroup;
using Neo.Plugins;

namespace FileStorageCLI
{
    public partial class CommandsPlugin : Plugin
    {
        [ConsoleCommand("fs object put", Category = "FileStorageService", Description = "Put a object")]
        private void OnPutObject(string containerId, string pdata, string paccount = null)
        {
            if (NoWallet()) return;
            UInt160 account = paccount is null ? currentWallet.GetAccounts().Where(p => !p.WatchOnly).ToArray()[0].ScriptHash : UInt160.Parse(paccount);
            if (!CheckAccount(account)) return;
            var data = UTF8Encoding.UTF8.GetBytes(pdata);
            if (data.Length > 2048)
            {
                Console.WriteLine("The data is too big");
                return;
            }
            var host = Settings.Default.host;
            ECDsa key = currentWallet.GetAccount(account).GetKey().Export().LoadWif();
            using (var client = new Client(key, host))
            {
                var cid = ContainerID.FromBase58String(containerId);
                var obj = new Neo.FileStorage.API.Object.Object
                {
                    Header = new Header
                    {
                        OwnerId = OwnerID.FromScriptHash(key.PublicKey().PublicKeyToScriptHash()),
                        ContainerId = cid,
                    },
                    Payload = ByteString.CopyFrom(data),
                };
                obj.ObjectId = obj.CalculateID();
                var source1 = new CancellationTokenSource();
                source1.CancelAfter(TimeSpan.FromMinutes(1));
                var session = client.CreateSession(ulong.MaxValue, context: source1.Token).Result;
                source1.Cancel();
                var source2 = new CancellationTokenSource();
                source2.CancelAfter(TimeSpan.FromMinutes(1));
                var objId = client.PutObject(obj, new CallOptions { Ttl = 2, Session = session }, source2.Token).Result;
                Console.WriteLine($"The object put successfully, ObjectID:{objId.ToBase58String()}");
            }
        }

        [ConsoleCommand("fs object delete", Category = "FileStorageService", Description = "Delete a object")]
        private void OnDeleteObject(string containerId, string pobjectIds, string paccount = null)
        {
            if (NoWallet()) return;
            UInt160 account = paccount is null ? currentWallet.GetAccounts().Where(p => !p.WatchOnly).ToArray()[0].ScriptHash : UInt160.Parse(paccount);
            if (!CheckAccount(account)) return;
            var host = Settings.Default.host;
            ECDsa key = currentWallet.GetAccount(account).GetKey().Export().LoadWif();
            using (var client = new Client(key, host))
            {
                var cid = ContainerID.FromBase58String(containerId);
                string[] objectIds = pobjectIds.Split("_");
                foreach (var objectId in objectIds)
                {
                    var oid = ObjectID.FromBase58String(objectId);
                    Address address = new Address(cid, oid);
                    var source1 = new CancellationTokenSource();
                    source1.CancelAfter(TimeSpan.FromMinutes(1));
                    var session = client.CreateSession(ulong.MaxValue, context: source1.Token).Result;
                    source1.Cancel();
                    var source2 = new CancellationTokenSource();
                    source2.CancelAfter(TimeSpan.FromMinutes(1));
                    var objId = client.DeleteObject(address, new CallOptions { Ttl = 2, Session = session }, source2.Token).Result;
                    Console.WriteLine($"The object delete successfully,ObjectID:{objId}");
                }
            }
        }


        [ConsoleCommand("fs object get", Category = "FileStorageService", Description = "Get a object")]
        private void OnGetObject(string containerId, string objectId, string paccount = null)
        {
            if (NoWallet()) return;
            UInt160 account = paccount is null ? currentWallet.GetAccounts().Where(p => !p.WatchOnly).ToArray()[0].ScriptHash : UInt160.Parse(paccount);
            if (!CheckAccount(account)) return;
            var host = Settings.Default.host;
            ECDsa key = currentWallet.GetAccount(account).GetKey().Export().LoadWif();
            using (var client = new Client(key, host))
            {
                var cid = ContainerID.FromBase58String(containerId);
                var oid = ObjectID.FromBase58String(objectId);
                var source = new CancellationTokenSource();
                source.CancelAfter(TimeSpan.FromMinutes(1));
                var obj = client.GetObject(new()
                {
                    ContainerId = cid,
                    ObjectId = oid,
                }, false, new CallOptions { Ttl = 2 }, source.Token).Result;
                Console.WriteLine($"Object info:{obj.ToJson()}");
                /*                Console.WriteLine($"ObjectId:{obj.ObjectId.ToBase58String()}");
                                Console.WriteLine($"ObjectPayload:{obj.Payload.ToByteArray()}");
                                if (obj.ObjectType == ObjectType.StorageGroup)
                                {
                                    var sg = StorageGroup.Parser.ParseFrom(obj.Payload.ToByteArray());
                                    Console.WriteLine($"StorageGroup size: {sg.ValidationDataSize}");
                                    Console.WriteLine($"StorageGroup members:");
                                    foreach (var m in sg.Members)
                                        Console.WriteLine(m.ToBase58String());
                                }*/
            }
        }

        [ConsoleCommand("fs storagegroup object put", Category = "FileStorageService", Description = "Put a storage object")]
        private void OnStorageGroupObject(string containerId, string pobjectIds, string paccount = null)
        {
            if (NoWallet()) return;
            UInt160 account = paccount is null ? currentWallet.GetAccounts().Where(p => !p.WatchOnly).ToArray()[0].ScriptHash : UInt160.Parse(paccount);
            if (!CheckAccount(account)) return;
            string[] objectIds = pobjectIds.Split("_");
            var host = Settings.Default.host;
            ECDsa key = currentWallet.GetAccount(account).GetKey().Export().LoadWif();
            using (var client = new Client(key, host))
            {
                var cid = ContainerID.FromBase58String(containerId);
                List<ObjectID> oids = objectIds.Select(p => ObjectID.FromBase58String(p)).ToList();
                byte[] tzh = null;
                ulong size = 0;
                foreach (var oid in oids)
                {
                    var address = new Address(cid, oid);
                    var source = new CancellationTokenSource();
                    source.CancelAfter(TimeSpan.FromMinutes(1));
                    var oo = client.GetObject(address, false, new CallOptions { Ttl = 2 }, source.Token).Result;
                    if (tzh is null)
                        tzh = oo.PayloadHomomorphicHash.Sum.ToByteArray();
                    else
                        tzh = TzHash.Concat(new() { tzh, oo.PayloadHomomorphicHash.Sum.ToByteArray() });
                    size += oo.PayloadSize;
                }
                var epoch = client.Epoch().Result;
                StorageGroup sg = new()
                {
                    ValidationDataSize = size,
                    ValidationHash = new()
                    {
                        Type = ChecksumType.Tz,
                        Sum = ByteString.CopyFrom(tzh)
                    },
                    ExpirationEpoch = epoch + 100,
                };
                sg.Members.AddRange(oids);
                var obj = new Neo.FileStorage.API.Object.Object
                {
                    Header = new Header
                    {
                        OwnerId = OwnerID.FromScriptHash(key.PublicKey().PublicKeyToScriptHash()),
                        ContainerId = cid,
                        ObjectType = ObjectType.StorageGroup,
                    },
                    Payload = ByteString.CopyFrom(sg.ToByteArray()),
                };
                obj.ObjectId = obj.CalculateID();
                var source1 = new CancellationTokenSource();
                source1.CancelAfter(TimeSpan.FromMinutes(1));
                var session = client.CreateSession(ulong.MaxValue, context: source1.Token).Result;
                source1.Cancel();
                var source2 = new CancellationTokenSource();
                source2.CancelAfter(TimeSpan.FromMinutes(1));
                var o = client.PutObject(obj, new CallOptions { Ttl = 2, Session = session }, source2.Token).Result;
                Console.WriteLine($"The storagegroup object put successfully,ObjectID:{o.ToBase58String()}");
            }
        }
    }
}
