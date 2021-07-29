using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Google.Protobuf;
using Neo;
using Neo.ConsoleService;
using Neo.Cryptography;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Cryptography.Tz;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.Session;
using Neo.FileStorage.API.StorageGroup;
using Neo.Plugins;

namespace FileStorageCLI
{
    public partial class CommandsPlugin : Plugin
    {
        [ConsoleCommand("fs object put", Category = "FileStorageService", Description = "Put a object")]
        private void OnPutObject(string containerId, string pdata, string paccount = null)
        {
            if (!CheckAndParseAccount(paccount, out _, out ECDsa key)) return;
            if (pdata.Length > 2048 || pdata.Length < 1024)
            {
                Console.WriteLine("The data length out of range");
                return;
            }
            var data = UTF8Encoding.UTF8.GetBytes(pdata);
            using var client = new Client(key, Host);
            var cid = ContainerID.FromBase58String(containerId);
            var obj = OnCreateObjectInternal(cid, key, data, ObjectType.Regular);
            if (OnPutObjectInternal(client, obj))
                Console.WriteLine($"The object put successfully, ObjectID:{obj.ObjectId.ToBase58String()}");
        }

        [ConsoleCommand("fs object delete", Category = "FileStorageService", Description = "Delete a object")]
        private void OnDeleteObject(string containerId, string pobjectIds, string paccount = null)
        {
            if (!CheckAndParseAccount(paccount, out _, out ECDsa key)) return;
            using var client = new Client(key, Host);
            SessionToken session = OnCreateSessionInternal(client);
            if (session is null) return;
            var cid = ContainerID.FromBase58String(containerId);
            string[] objectIds = pobjectIds.Split("_");
            foreach (var objectId in objectIds)
            {
                var oid = ObjectID.FromBase58String(objectId);
                Address address = new Address(cid, oid);
                using var source = new CancellationTokenSource();
                source.CancelAfter(TimeSpan.FromMinutes(1));
                var objId = client.DeleteObject(address, new CallOptions { Ttl = 2, Session = session }, source.Token).Result;
                source.Cancel();
                Console.WriteLine($"The object delete successfully,ObjectID:{objId}");
            }
        }

        [ConsoleCommand("fs object get", Category = "FileStorageService", Description = "Get a object")]
        private void OnGetObject(string containerId, string objectId, string paccount = null)
        {
            if (!CheckAndParseAccount(paccount, out _, out ECDsa key)) return;
            using var client = new Client(key, Host);
            var cid = ContainerID.FromBase58String(containerId);
            var oid = ObjectID.FromBase58String(objectId);
            var obj = OnGetObjectInternal(client, cid, oid);
            if (obj is null) return;
            Console.WriteLine($"Object info:{obj.ToJson()}");
        }

        [ConsoleCommand("fs storagegroup object put", Category = "FileStorageService", Description = "Put a storage object")]
        private void OnStorageGroupObject(string containerId, string pobjectIds, string paccount = null)
        {
            if (!CheckAndParseAccount(paccount, out UInt160 account, out ECDsa key)) return;
            string[] objectIds = pobjectIds.Split("_");
            using var client = new Client(key, Host);
            SessionToken session = OnCreateSessionInternal(client);
            if (session is null) return;
            var cid = ContainerID.FromBase58String(containerId);
            List<ObjectID> oids = objectIds.Select(p => ObjectID.FromBase58String(p)).ToList();
            var obj = OnCreateStorageGroupObjectInternal(client, key, cid, oids.ToArray(), session);
            if (OnPutObjectInternal(client, obj, session)) Console.WriteLine($"The storagegroup object put successfully,ObjectID:{obj.ObjectId.ToBase58String()}");
        }

        private SessionToken OnCreateSessionInternal(Client client)
        {
            using var source = new CancellationTokenSource();
            source.CancelAfter(TimeSpan.FromMinutes(1));
            try
            {
                var session = client.CreateSession(ulong.MaxValue, context: source.Token).Result;
                return session;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Create session fail,error:{e}");
                source.Cancel();
                return null;
            }
        }

        private Neo.FileStorage.API.Object.Object OnCreateStorageGroupObjectInternal(Client client, ECDsa key, ContainerID cid, ObjectID[] oids, SessionToken session = null)
        {
            if (session is null)
                session = OnCreateSessionInternal(client);
            if (session is null) return null;
            byte[] tzh = null;
            ulong size = 0;
            foreach (var oid in oids)
            {
                var oo = OnGetObjectHeaderInternal(client, cid, oid);
                if (oo is null) return null;
                if (tzh is null)
                    tzh = oo.PayloadHomomorphicHash.Sum.ToByteArray();
                else
                    tzh = TzHash.Concat(new() { tzh, oo.PayloadHomomorphicHash.Sum.ToByteArray() });
                size += oo.PayloadSize;
            }
            if (!OnGetEpochInternal(client, out var epoch)) return null;
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
            return OnCreateObjectInternal(cid, key, sg.ToByteArray(), ObjectType.StorageGroup, session);
        }

        private Neo.FileStorage.API.Object.Object OnCreateObjectInternal(ContainerID cid, ECDsa key, byte[] data, ObjectType objectType, SessionToken session = null)
        {
            var obj = new Neo.FileStorage.API.Object.Object
            {
                Header = new Header
                {
                    Version = Neo.FileStorage.API.Refs.Version.SDKVersion(),
                    OwnerId = OwnerID.FromScriptHash(key.PublicKey().PublicKeyToScriptHash()),
                    ContainerId = cid,
                    ObjectType = objectType,
                    PayloadHash = new Checksum
                    {
                        Type = ChecksumType.Sha256,
                        Sum = ByteString.CopyFrom(data.Sha256()),
                    },
                    HomomorphicHash = new Checksum
                    {
                        Type = ChecksumType.Tz,
                        Sum = ByteString.CopyFrom(new TzHash().ComputeHash(data)),
                    },
                    PayloadLength = (ulong)data.Length,
                    SessionToken = session
                },
                Payload = ByteString.CopyFrom(data),
            };
            obj.ObjectId = obj.CalculateID();
            obj.Signature = obj.CalculateIDSignature(key);
            return obj;
        }

        private Neo.FileStorage.API.Object.Object OnGetObjectHeaderInternal(Client client, ContainerID cid, ObjectID oid, bool logFlag = true)
        {
            using var source = new CancellationTokenSource();
            source.CancelAfter(TimeSpan.FromMinutes(1));
            try
            {
                var objheader = client.GetObjectHeader(new Address()
                {
                    ContainerId = cid,
                    ObjectId = oid
                }, options: new CallOptions { Ttl = 2 }, context: source.Token).Result;
                source.Cancel();
                return objheader;
            }
            catch (Exception e)
            {
                if (logFlag) Console.WriteLine($"Get object header fail,objectId:{oid.ToBase58String()},error:{e}");
                source.Cancel();
                return null;
            }
        }

        private Neo.FileStorage.API.Object.Object OnGetObjectInternal(Client client, ContainerID cid, ObjectID oid)
        {
            using var source = new CancellationTokenSource();
            source.CancelAfter(TimeSpan.FromMinutes(1));
            try
            {
                var obj = client.GetObject(new Address()
                {
                    ContainerId = cid,
                    ObjectId = oid
                }, options: new CallOptions { Ttl = 2 }, context: source.Token).Result;
                source.Cancel();
                return obj;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Get object fail,objectId:{oid.ToBase58String()},error:{e}");
                source.Cancel();
                return null;
            }
        }

        private bool OnPutObjectInternal(Client client, Neo.FileStorage.API.Object.Object obj, SessionToken session = null)
        {
            if (session is null)
                session = OnCreateSessionInternal(client);
            if (session is null) return false;
            using var source = new CancellationTokenSource();
            source.CancelAfter(TimeSpan.FromMinutes(1));
            try
            {
                var o = client.PutObject(obj, new CallOptions { Ttl = 2, Session = session }, source.Token).Result;
                source.Cancel();
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Object put fail, errot:{e}");
                source.Cancel();
                return false;
            }
        }
    }
}
