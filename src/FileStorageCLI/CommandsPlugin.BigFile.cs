using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Neo;
using Neo.Cryptography;
using Neo.ConsoleService;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.StorageGroup;
using Neo.Plugins;

namespace FileStorageCLI
{
    public partial class CommandsPlugin : Plugin
    {
        private static string Host => Settings.Default.host;
        private static string DownloadPath => Settings.Default.downloadPath;

        [ConsoleCommand("fs file upload", Category = "FileStorageService", Description = "Upload file")]
        private void OnUploadFile(string containerId, string fileName, string paccount = null)
        {
            if (!CheckAndParseAccount(paccount, out UInt160 account, out ECDsa key, out Neo.Cryptography.ECC.ECPoint pk, out OwnerID ownerID)) return;
            var cid = ContainerID.FromBase58String(containerId);
            var filePath = Settings.Default.uploadPath + fileName;
            FileInfo fileInfo = new FileInfo(filePath);
            var FileLength = fileInfo.Length;
            //data segmentation
            long PackCount = 0;
            int PackSize = 2 * 1024 * 1000;
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
                    using var client = new Client(key, Host);
                    var session = OnCreateSessionInternal(client);
                    if (session is null) return;
                    int i = 0;
                    while (threadIndex + i * taskCounts < subObjectIDs.Length)
                    {
                        byte[] data = OnGetFileInternal(filePath, (threadIndex + i * taskCounts) * PackSize, PackSize, FileLength);
                        var obj = OnCreateObjectInternal(cid, OwnerID.FromScriptHash(key.PublicKey().PublicKeyToScriptHash()), data, ObjectType.Regular);
                        //check has upload;
                        var objheader = OnGetObjectHeaderInternal(client, cid, obj.ObjectId);
                        if (objheader is not null || objheader is null && OnPutObjectInternal(client, obj, session))
                        {
                            Console.WriteLine($"The object put request has been submitted, please confirm in the next block,ObjectID:{obj.ObjectId.ToBase58String()},degree of completion:{Interlocked.Increment(ref completedTaskCount)}/{PackCount}");
                            subObjectIDs[threadIndex + i * taskCounts] = obj.ObjectId;
                        }
                        i++;
                        Thread.Sleep(1000);
                    }
                });
                tasks[index] = task;
                task.Start();
            }
            Task.WaitAll(tasks);
            //check failed task
            for (int i = 0; i < subObjectIDs.Length; i++)
            {
                if (subObjectIDs[i] is null)
                {
                    Console.WriteLine("Some data upload fault.Please upload again.");
                    return;
                }
            }
            //upload storagegroup object
            using var client = new Client(key, Host);
            var session = OnCreateSessionInternal(client);
            if (session is null) return;
            var obj = OnCreateStorageGroupObjectInternal(client, OwnerID.FromScriptHash(key.PublicKey().PublicKeyToScriptHash()), cid, subObjectIDs, session);
            if (OnPutObjectInternal(client, obj, session)) Console.WriteLine("Upload file successfully");
        }

        [ConsoleCommand("fs file download", Category = "FileStorageService", Description = "Download file")]
        private void OnDownloadFile(string containerId, string objectId, string fileName, bool cacheFlag = false, string paccount = null)
        {
            if (!CheckAndParseAccount(paccount, out UInt160 account, out ECDsa key, out Neo.Cryptography.ECC.ECPoint pk, out OwnerID ownerID)) return;
            var subObjectIDs = new List<ObjectID>();
            var cid = ContainerID.FromBase58String(containerId);
            //download storagegroup object
            var totalDataSize = 0ul;
            using var client = new Client(key, Host);
            var oid = ObjectID.FromBase58String(objectId);
            var obj = OnGetObjectInternal(client, cid, oid);
            if (obj is null || obj.ObjectType != ObjectType.StorageGroup)
            {
                Console.WriteLine("Missing file index, please provide the correct objectid");
                return;
            }
            var sg = StorageGroup.Parser.ParseFrom(obj.Payload.ToByteArray());
            totalDataSize = sg.ValidationDataSize;
            Console.WriteLine($"Download file index successfully");
            Console.WriteLine($"File objects size: {totalDataSize}");
            Console.WriteLine($"File subobject list:");
            foreach (var m in sg.Members)
            {
                subObjectIDs.Add(m);
                Console.WriteLine($"subobjectId:{m.ToBase58String()}");
            }

            var downloadTempPath = DownloadPath + objectId + "/";
            if (!Directory.Exists(downloadTempPath)) Directory.CreateDirectory(downloadTempPath);
            Console.WriteLine("Start file download");
            var receivedDataSize = 0uL;
            var taskCounts = 10;
            var tasks = new Task[taskCounts];
            for (int index = 0; index < taskCounts; index++)
            {
                var threadIndex = index;
                var task = new Task(() =>
               {
                   using var client = new Client(key, Host);
                   int i = 0;
                   while (threadIndex + i * taskCounts < subObjectIDs.Count)
                   {
                       string tempfilepath = downloadTempPath + "QS_" + subObjectIDs[threadIndex + i * taskCounts].ToBase58String();
                       FileInfo tempfile = new FileInfo(tempfilepath);
                       if (tempfile.Exists)
                       {
                           using FileStream tempstream = new FileStream(tempfilepath, FileMode.Open);
                           byte[] downedData = new byte[tempstream.Length];
                           tempstream.Read(downedData, 0, downedData.Length);
                           var oid = subObjectIDs[threadIndex + i * taskCounts];
                           var objheader = OnGetObjectHeaderInternal(client, cid, oid);
                           if (objheader is null) continue;
                           if (downedData.Sha256().SequenceEqual(objheader.PayloadChecksum.Sum.ToByteArray()))
                           {
                               Console.WriteLine($"Download subobject successfully,objectId:{oid.ToBase58String()},degree of completion:{Interlocked.Add(ref receivedDataSize, (ulong)downedData.Length)}/{totalDataSize}");
                               continue;
                           }
                           else
                               tempfile.Delete();
                       }
                       else
                       {
                           using FileStream tempstream = new FileStream(tempfilepath, FileMode.Create, FileAccess.Write, FileShare.Write);
                           var oid = subObjectIDs[threadIndex + i * taskCounts];
                           var obj = OnGetObjectInternal(client, cid, oid);
                           if (obj is null) return;
                           var payload = obj.Payload.ToByteArray();
                           tempstream.Write(payload, 0, payload.Length);
                           tempstream.Flush();
                           tempstream.Close();
                           tempstream.Dispose();
                           Console.WriteLine($"Download subobject successfully,objectId:{oid.ToBase58String()},degree of completion:{Interlocked.Add(ref receivedDataSize, (ulong)payload.Length)}/{totalDataSize}");
                       }
                       i++;
                   }
               });
                tasks[index] = task;
                task.Start();
            }
            Task.WaitAll(tasks);
            //check failed task
            DirectoryInfo tempDownLoadDir = new DirectoryInfo(downloadTempPath);
            List<ObjectID> Comparefiles = new List<ObjectID>();
            for (int i = 0; i < subObjectIDs.Count; i++)
            {
                bool hasfile = false;
                foreach (FileInfo Tempfile in tempDownLoadDir.GetFiles())
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
                if (!cacheFlag) tempDownLoadDir.Delete(true);
                return;
            }
            //write file
            string filePath = DownloadPath + fileName;
            using (FileStream writestream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Write))
            {
                for (int index = 0; index < subObjectIDs.Count; index++)
                {
                    string tempfilepath = downloadTempPath + "QS_" + subObjectIDs[index].ToBase58String();
                    FileInfo Tempfile = new FileInfo(tempfilepath);
                    using FileStream readTempStream = new FileStream(Tempfile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    long onefileLength = Tempfile.Length;
                    byte[] buffer = new byte[Convert.ToInt32(onefileLength)];
                    readTempStream.Read(buffer, 0, Convert.ToInt32(onefileLength));
                    writestream.Write(buffer, 0, Convert.ToInt32(onefileLength));
                }
                writestream.Flush();
                writestream.Close();
                writestream.Dispose();
            }
            //delete temp file
            tempDownLoadDir.Delete(true);
            Console.WriteLine("Download file successfully");
        }

        private byte[] OnGetFileInternal(string filePath, long start, int length, long totalLength)
        {
            using FileStream ServerStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1024 * 80, true);
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
