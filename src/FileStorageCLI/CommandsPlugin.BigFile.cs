using Neo.Plugins;
using Neo.ConsoleService;
using System;
using Neo;
using System.Linq;
using Neo.FileStorage.API.Cryptography;
using System.Security.Cryptography;
using Neo.FileStorage.API.Client;
using System.Threading;
using Neo.FileStorage.API.Refs;
using System.Collections.Generic;
using Google.Protobuf;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.StorageGroup;
using Neo.FileStorage.API.Cryptography.Tz;
using System.IO;
using System.Threading.Tasks;

namespace FileStorageCLI
{
    public partial class CommandsPlugin : Plugin
    {
        [ConsoleCommand("fs file upload", Category = "FileStorageService", Description = "Upload file")]
        private void OnUploadFile(string containerId, string fileName, string paccount = null)
        {
            if (NoWallet()) return;
            UInt160 account = paccount is null ? currentWallet.GetAccounts().Where(p => !p.WatchOnly).ToArray()[0].ScriptHash : UInt160.Parse(paccount);
            if (!CheckAccount(account)) return;
            var host = Settings.Default.host;
            ECDsa key = currentWallet.GetAccount(account).GetKey().Export().LoadWif();
            var cid = ContainerID.FromBase58String(containerId);
            var filePath = Settings.Default.uploadPath + fileName;
            FileInfo fileInfo = new FileInfo(filePath);
            var FileLength = fileInfo.Length;
            //data segmentation
            long PackCount = 0;
            int PackSize = 1024 * 1000;
            if (FileLength % PackSize > 0)
                PackCount = (int)(FileLength / PackSize) + 1;
            else
                PackCount = (int)(FileLength / PackSize);
            //upload subobjects
            var subObjectIDs = new ObjectID[PackCount];
            var completedTaskCount = 0;
            var taskCounts = 10;
            var tasks = new Task[taskCounts];
            for (int index = 0; index < taskCounts; index++)
            {
                var threadIndex = index;
                var task = new Task(() =>
                {
                    int i = 0;
                    while (threadIndex + i * taskCounts < subObjectIDs.Length)
                    {
                        byte[] data = GetFile(filePath, (threadIndex + i * taskCounts) * PackSize, PackSize, FileLength);
                        using (var client = new Client(key, host))
                        {
                            var obj = new Neo.FileStorage.API.Object.Object
                            {
                                Header = new Header
                                {
                                    OwnerId = key.ToOwnerID(),
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
                            Console.WriteLine($"The object put request has been submitted, please confirm in the next block,ObjectID:{objId.ToBase58String()},degree of completion:{Interlocked.Increment(ref completedTaskCount)}/{PackCount}");
                            subObjectIDs[threadIndex + i * taskCounts] = objId;
                        }
                        i++;
                    }
                });
                tasks[index] = task;
                task.Start();
            }
            Task.WaitAll(tasks);
            //check failed task
            for (int i = 0; i < subObjectIDs.Length; i++)
            {
                if (subObjectIDs[i] is null) return;
            }
            //upload storagegroup object
            using (var client = new Client(key, host))
            {
                byte[] tzh = null;
                ulong size = 0;
                foreach (var oid in subObjectIDs)
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
                sg.Members.AddRange(subObjectIDs);
                var obj = new Neo.FileStorage.API.Object.Object
                {
                    Header = new Header
                    {
                        OwnerId = key.ToOwnerID(),
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
                Console.WriteLine($"The storagegroup object put request has been submitted, please confirm in the next block,ObjectID:{o.ToBase58String()}");
            }
            Console.WriteLine("Upload file successfully");
        }

        [ConsoleCommand("fs file download", Category = "FileStorageService", Description = "Download file")]
        private void OnDownloadFile(string containerId, string objectId, string fileName, string paccount = null)
        {
            if (NoWallet()) return;
            UInt160 account = paccount is null ? currentWallet.GetAccounts().Where(p => !p.WatchOnly).ToArray()[0].ScriptHash : UInt160.Parse(paccount);
            if (!CheckAccount(account)) return;
            var host = Settings.Default.host;
            var downloadPath = Settings.Default.downloadPath;
            ECDsa key = currentWallet.GetAccount(account).GetKey().Export().LoadWif();
            var subObjectIDs = new List<ObjectID>();
            var cid = ContainerID.FromBase58String(containerId);
            //download storagegroup object
            var totalDataSize = 0ul;
            using (var client = new Client(key, host))
            {
                var oid = ObjectID.FromBase58String(objectId);
                var source = new CancellationTokenSource();
                source.CancelAfter(TimeSpan.FromMinutes(1));
                var obj = client.GetObject(new()
                {
                    ContainerId = cid,
                    ObjectId = oid,
                }, false, new CallOptions { Ttl = 2 }, source.Token).Result;
                if (obj.ObjectType != ObjectType.StorageGroup)
                {
                    Console.WriteLine("Missing file storagegroup object, please provide the correct objectid");
                    return;
                }
                var sg = StorageGroup.Parser.ParseFrom(obj.Payload.ToByteArray());
                totalDataSize = sg.ValidationDataSize;
                Console.WriteLine($"Download storage group successfully");
                Console.WriteLine($"File objects size: {totalDataSize}");
                Console.WriteLine($"File subobject list:");
                foreach (var m in sg.Members)
                {
                    subObjectIDs.Add(m);
                    Console.WriteLine($"subobjectId:{m.ToBase58String()}");
                }
            }
            var downloadTempPath = Settings.Default.downloadPath + objectId + "/";
            if (!Directory.Exists(downloadTempPath)) Directory.CreateDirectory(downloadTempPath);
            Console.WriteLine("Start file subobjects download");
            var receivedDataSize = 0uL;
            var taskCounts = 10;
            var tasks = new Task[taskCounts];
            for (int index = 0; index < taskCounts; index++)
            {
                var threadIndex = index;
                var task = new Task(() =>
               {
                   int i = 0;
                   while (threadIndex + i * taskCounts < subObjectIDs.Count)
                   {
                       string tempfilepath = downloadTempPath + "QS_" + subObjectIDs[threadIndex + i * taskCounts].ToBase58String();
                       using (FileStream tempstream = new FileStream(tempfilepath, FileMode.Create, FileAccess.Write, FileShare.Write))
                       {
                           using (var client = new Client(key, host))
                           {
                               var oid = subObjectIDs[index + i * taskCounts];
                               var source = new CancellationTokenSource();
                               source.CancelAfter(TimeSpan.FromMinutes(1));
                               var obj = client.GetObject(new()
                               {
                                   ContainerId = cid,
                                   ObjectId = oid,
                               }, false, new CallOptions { Ttl = 2 }, source.Token).Result;
                               var payload = obj.Payload.ToByteArray();
                               tempstream.Write(payload, 0, payload.Length);
                               tempstream.Flush();
                               tempstream.Close();
                               tempstream.Dispose();
                               Console.WriteLine($"Download subobject successfully,objectId:{oid.ToBase58String()},degree of completion:{Interlocked.Add(ref receivedDataSize, (ulong)payload.Length)}/{totalDataSize}");
                           }
                       }
                       i++;
                   }
               });
                tasks[index] = task;
                task.Start();
            }
            Task.WaitAll(tasks);
            //check failed task
            DirectoryInfo TempDir = new DirectoryInfo(downloadTempPath);
            List<ObjectID> Comparefiles = new List<ObjectID>();
            for (int i = 0; i < subObjectIDs.Count; i++)
            {
                bool hasfile = false;
                foreach (FileInfo Tempfile in TempDir.GetFiles())
                {
                    if (Tempfile.Name.Split('_')[1] == subObjectIDs[i].ToBase58String())
                    {
                        hasfile = true;
                        break;
                    }
                }
                if (hasfile == false)
                {
                    Comparefiles.Add(subObjectIDs[i]);
                }
            }
            if (Comparefiles.Count > 0)
            {
                Console.WriteLine($"Some data is missing, please download again");
                return;
                /*                foreach (ObjectID com_objectId in Comparefiles)
                                {
                                    string tempfilepath = downloadTempPath + "QS_" +com_objectId.ToBase58String();
                                    using (FileStream Compstream = new FileStream(tempfilepath, FileMode.Create, FileAccess.Write, FileShare.Write))
                                    {
                                        using (var client = new Client(key, host))
                                        {
                                            var oid = com_objectId;
                                            var source = new CancellationTokenSource();
                                            source.CancelAfter(TimeSpan.FromMinutes(1));
                                            var obj = client.GetObject(new()
                                            {
                                                ContainerId = cid,
                                                ObjectId = oid,
                                            }, false, new CallOptions { Ttl = 2 }, source.Token).Result;
                                            var payload = obj.Payload.ToByteArray();
                                            Compstream.Write(payload, 0, payload.Length);
                                            Compstream.Flush();
                                            Compstream.Close();
                                            Compstream.Dispose();
                                            Console.WriteLine($"File subject object:{oid.ToBase58String()} download complete");
                                        }
                                    }
                                }*/
            }
            //write file
            string filePath = Settings.Default.downloadPath + fileName;
            using (FileStream writestream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Write))
            {
                for (int index = 0; index < subObjectIDs.Count; index++)
                {
                    string tempfilepath = downloadTempPath + "QS_" + subObjectIDs[index].ToBase58String();
                    FileInfo Tempfile = new FileInfo(tempfilepath);
                    using (FileStream readTempStream = new FileStream(Tempfile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        long onefileLength = Tempfile.Length;
                        byte[] buffer = new byte[Convert.ToInt32(onefileLength)];
                        readTempStream.Read(buffer, 0, Convert.ToInt32(onefileLength));
                        writestream.Write(buffer, 0, Convert.ToInt32(onefileLength));
                    }
                }
                writestream.Flush();
                writestream.Close();
                writestream.Dispose();
            }
            //delete temp file
            foreach (FileInfo Tempfile in TempDir.GetFiles())
            {
                Tempfile.Delete();
            }
            Console.WriteLine("Download file successfully");
        }

        private static byte[] GetFile(string filePath, long start, int length, long totalLength)
        {
            using (FileStream ServerStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1024 * 80, true))
            {
                byte[] buffer;
                //ServerStream.Position = start;
                ServerStream.Seek(start, SeekOrigin.Begin);
                if (totalLength - start < length)
                {
                    buffer = new byte[totalLength - start];
                    ServerStream.Read(buffer, 0, (int)(totalLength - start));
                }
                else
                {
                    buffer = new byte[length];
                    ServerStream.Read(buffer, 0, length);
                }
                return buffer;
            }
        }
    }
}
