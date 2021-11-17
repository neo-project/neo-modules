using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
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
using Neo.IO.Json;
using Neo.Plugins;

namespace FileStorageCLI
{
    public partial class CommandsPlugin : Plugin
    {
        /// <summary>
        /// User can invoke this command to create a object.
        /// </summary>
        /// <param name="containerId">containerId</param>
        /// <param name="pdata">data,size [1K,2M]</param>
        /// <param name="paddress">account address(The first account of the wallet,default)</param>
        [ConsoleCommand("fs object put", Category = "FileStorageService", Description = "Put a object")]
        private void OnPutObject(string containerId, string pdata, string paddress = null)
        {
            if (!CheckAndParseAccount(paddress, out _, out ECDsa key)) return;
            if (pdata.Length > 2048 * 1000 || pdata.Length < 1024) throw new Exception("The data length out of range");
            var data = Utility.StrictUTF8.GetBytes(pdata);
            using var client = OnCreateClientInternal(key);
            if (client is null) return;
            if (!ParseContainerID(containerId, out var cid)) return;
            var obj = OnCreateObjectInternal(cid, key, data, ObjectType.Regular);
            if (OnPutObjectInternal(client, obj))
                Console.WriteLine($"The object put successfully, ObjectID:{obj.ObjectId.String()}");
        }

        /// <summary>
        /// User can invoke this command to delete a object.
        /// </summary>
        /// <param name="containerId">containerId</param>
        /// <param name="pobjectIds">pobjectIds,support batch delete,split by "_"</param>
        /// <param name="paddress">account address(The first account of the wallet,default)</param>
        [ConsoleCommand("fs object delete", Category = "FileStorageService", Description = "Delete a object")]
        private void OnDeleteObject(string containerId, string pobjectIds, string paddress = null)
        {
            if (!CheckAndParseAccount(paddress, out _, out ECDsa key)) return;
            using var client = OnCreateClientInternal(key);
            if (client is null) return;
            SessionToken session = OnCreateSessionInternal(client);
            if (session is null) return;
            if (!ParseContainerID(containerId, out var cid)) return;
            string[] objectIds = pobjectIds.Split("_");
            foreach (var objectId in objectIds)
            {
                if (!ParseObjectID(objectId, out var oid)) return;
                Address address = new(cid, oid);
                using CancellationTokenSource source = new();
                source.CancelAfter(TimeSpan.FromMinutes(1));
                try
                {
                    var objId = client.DeleteObject(address, new CallOptions { Ttl = 2, Session = session }, source.Token).Result;
                    source.Cancel();
                    Console.WriteLine($"The object delete successfully,ObjectID:{objId}");
                }
                catch (Exception e)
                {
                    source.Cancel();
                    Console.WriteLine($"The object delete fault,error:{e}");
                }
            }
        }

        /// <summary>
        /// User can invoke this command to query object.
        /// </summary>
        /// <param name="containerId">containerId</param>
        /// <param name="objectId">objectId</param>
        /// <param name="paddress">account address(The first account of the wallet,default)</param>
        [ConsoleCommand("fs object get", Category = "FileStorageService", Description = "Get a object")]
        private void OnGetObject(string containerId, string objectId, string paddress = null)
        {
            if (!CheckAndParseAccount(paddress, out _, out ECDsa key)) return;
            using var client = OnCreateClientInternal(key);
            if (client is null) return;
            if (!ParseContainerID(containerId, out var cid)) return;
            if (!ParseObjectID(objectId, out var oid)) return;
            var obj = OnGetObjectInternal(client, cid, oid);
            if (obj is null) return;
            JArray result = new();
            result.Add(obj.ToJson());
            if (obj.ObjectType == ObjectType.StorageGroup)
            {
                List<string> subObjectIDs = new();
                var sg = StorageGroup.Parser.ParseFrom(obj.Payload.ToByteArray());
                foreach (var m in sg.Members)
                {
                    subObjectIDs.Add(m.String());
                }
                string.Join("_", subObjectIDs);
                JObject @object = new();
                @object["subIds"] = string.Join("_", subObjectIDs);
                result.Add(@object);
            }
            Console.WriteLine($"Object info:{result}");
        }

        /// <summary>
        /// User can invoke this command to get all object of a container.
        /// </summary>
        /// <param name="containerId">containerId</param>
        /// <param name="paddress">account address(The first account of the wallet,default)</param>
        [ConsoleCommand("fs object list", Category = "FileStorageService", Description = "list object")]
        private void OnListObject(string containerId, string paddress = null)
        {
            if (!CheckAndParseAccount(paddress, out _, out ECDsa key)) return;
            if (!ParseContainerID(containerId, out var cid)) return;
            using var client = OnCreateClientInternal(key);
            if (client is null) return;
            using CancellationTokenSource source = new();
            source.CancelAfter(TimeSpan.FromMinutes(1));
            var filter = new SearchFilters();
            try
            {
                List<ObjectID> objs = client.SearchObject(cid, filter, context: source.Token).Result;
                source.Cancel();
                Console.WriteLine($"list object,cid:{cid}");
                objs.ForEach(p => Console.WriteLine($"ObjectId:{p.String()}"));
            }
            catch (Exception e)
            {
                Console.WriteLine($"fs get object list fault,error:{e}");
                source.Cancel();
            }
        }

        /// <summary>
        /// User can invoke this command to create a storagegroup object.
        /// </summary>
        /// <param name="containerId">containerId</param>
        /// <param name="pobjectIds">subobjectIds,split "_"</param>
        /// <param name="paddress">account address(The first account of the wallet,default)</param>
        [ConsoleCommand("fs storagegroup object put", Category = "FileStorageService", Description = "Put a storage object")]
        private void OnStorageGroupObject(string containerId, string pobjectIds, string paddress = null)
        {
            if (!CheckAndParseAccount(paddress, out UInt160 account, out ECDsa key)) return;
            string[] objectIds = pobjectIds.Split("_");
            using var client = OnCreateClientInternal(key);
            if (client is null) return;
            SessionToken session = OnCreateSessionInternal(client);
            if (session is null) return;
            if (!ParseContainerID(containerId, out var cid)) return;
            List<ObjectID> oids = objectIds.Select(p => ObjectID.FromString(p)).ToList();
            var obj = OnCreateStorageGroupObjectInternal(client, key, cid, oids.ToArray());
            if (OnPutObjectInternal(client, obj, session)) Console.WriteLine($"The storagegroup object put successfully,ObjectID:{obj.ObjectId.String()}");
        }

        //internal function
        private Neo.FileStorage.API.Object.Object OnCreateStorageGroupObjectInternal(Client client, ECDsa key, ContainerID cid, ObjectID[] oids, Header.Types.Attribute[] attributes = null)
        {
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
            return OnCreateObjectInternal(cid, key, sg.ToByteArray(), ObjectType.StorageGroup, attributes);
        }

        private Neo.FileStorage.API.Object.Object OnCreateObjectInternal(ContainerID cid, ECDsa key, byte[] data, ObjectType objectType, Header.Types.Attribute[] attributes = null)
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
                },
                Payload = ByteString.CopyFrom(data),
            };
            if (attributes is not null) obj.Header.Attributes.AddRange(attributes);
            obj.ObjectId = obj.CalculateID();
            obj.Signature = obj.CalculateIDSignature(key);
            return obj;
        }

        private Neo.FileStorage.API.Object.Object OnGetObjectHeaderInternal(Client client, ContainerID cid, ObjectID oid, bool logFlag = true)
        {
            using CancellationTokenSource source = new();
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
                if (logFlag) Console.WriteLine($"Fs get object header fault,error:{e}");
                source.Cancel();
                return null;
            }
        }

        private Neo.FileStorage.API.Object.Object OnGetObjectInternal(Client client, ContainerID cid, ObjectID oid)
        {
            using CancellationTokenSource source = new();
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
                Console.WriteLine($"Fs get object fault,error:{e}");
                source.Cancel();
                return null;
            }
        }

        private bool OnPutObjectInternal(Client client, Neo.FileStorage.API.Object.Object obj, SessionToken session = null)
        {
            if (session is null)
                session = OnCreateSessionInternal(client);
            if (session is null) return false;
            using CancellationTokenSource source = new();
            source.CancelAfter(TimeSpan.FromMinutes(1));
            try
            {
                var o = client.PutObject(obj, new CallOptions { Ttl = 2, Session = session }, source.Token).Result;
                source.Cancel();
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Fs put object fault, error:{e}");
                source.Cancel();
                return false;
            }
        }
    }
}
