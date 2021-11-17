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
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.StorageGroup;
using Neo.Plugins;
using System.Diagnostics;
using System.Text;
using Neo.IO.Json;
using IO = System.IO.Path;

namespace FileStorageCLI
{
    public partial class CommandsPlugin : Plugin
    {
        private static char DirectorySeparatorChar = IO.DirectorySeparatorChar;
        private static Process UploadProcess;
        private static Process DownloadProcess;

        /// <summary>
        /// User can invoke this command to upload a bigfile.
        /// </summary>
        /// <param name="containerId">containerId</param>
        /// <param name="filePath">filePath</param>
        /// <param name="paddress">account address(The first account of the wallet,default)</param>
        /// <param name="again">whether to recover the last task</param>
        [ConsoleCommand("fs file upload", Category = "FileStorageService", Description = "Upload file")]
        private void OnUploadFile(string containerId, string filePath, string paddress = null, bool again = false)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            if (!CheckAndParseAccount(paddress, out UInt160 account, out ECDsa key)) return;
            if (!ParseContainerID(containerId, out var cid)) return;
            FileInfo fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists) throw new Exception($"The specified file does not exist");
            var FileLength = fileInfo.Length;
            //data segmentation
            long PackCount = 0;
            int PackSize = 2 * 1024 * 1000;
            if (FileLength % PackSize > 0)
                PackCount = (int)(FileLength / PackSize) + 1;
            else
                PackCount = (int)(FileLength / PackSize);
            using var client = OnCreateClientInternal(key);
            if (client is null) return;
            var session = OnCreateSessionInternal(client);
            if (session is null) return;
            //upload subobjects
            String timeStamp = null;
            Header.Types.Attribute[] attributes = null;
            if (UploadProcess is null || !again)
            {
                timeStamp = DateTimeOffset.Now.ToUnixTimeSeconds().ToString();
                UploadProcess = new Process(containerId, null, fileInfo.Name, filePath, (ulong)FileLength, timeStamp, PackCount);
            }
            else
            {
                if (UploadProcess.ContainerId != containerId || UploadProcess.FilePath != filePath) throw new Exception($"The new task doesn't match with the last failed one");
                UploadProcess.Rest();
                timeStamp = UploadProcess.TimeStamp;
            }
            attributes = new Header.Types.Attribute[] {
                               new Header.Types.Attribute() { Key = Header.Types.Attribute.AttributeFileName, Value =  fileInfo.Name },
                               new Header.Types.Attribute() { Key = Header.Types.Attribute.AttributeTimestamp, Value = timeStamp }
                        };
            var taskCounts = 10;
            var tasks = new Task[taskCounts];
            for (int index = 0; index < taskCounts; index++)
            {
                var threadIndex = index;
                var task = new Task(() =>
                {
                    using var internalClient = OnCreateClientInternal(key);
                    if (internalClient is null) return;
                    var internalSession = OnCreateSessionInternal(internalClient);
                    if (internalSession is null) return;
                    int i = 0;
                    while (threadIndex + i * taskCounts < UploadProcess.SubObjectIds.Length)
                    {
                        byte[] data = OnGetFileInternal(filePath, ((long)threadIndex + (long)i * (long)taskCounts) * (long)PackSize, PackSize, FileLength);
                        Neo.FileStorage.API.Object.Object obj = OnCreateObjectInternal(cid, key, data, ObjectType.Regular, attributes);
                        //check has upload;                        
                        if (UploadProcess.SubObjectIds[threadIndex + i * taskCounts] is not null || OnPutObjectInternal(internalClient, obj, internalSession))
                        {
                            UploadProcess.SubObjectIds[threadIndex + i * taskCounts] = obj.ObjectId;
                            Console.WriteLine($"The object put request has been submitted,ObjectID:{obj.ObjectId.String()},degree of completion:{UploadProcess.Add((ulong)obj.Header.PayloadLength)}/{FileLength}");
                        }
                        else break;
                        i++;
                        Thread.Sleep(500);
                    }
                });
                tasks[index] = task;
                task.Start();
            }
            Task.WaitAll(tasks);
            //check failed task
            for (int i = 0; i < UploadProcess.SubObjectIds.Length; i++)
            {
                if (UploadProcess.SubObjectIds[i] is null)
                {
                    Console.WriteLine("Fs file upload fault.Please upload again.");
                    return;
                }
            }
            //upload storagegroup object
            var obj = OnCreateStorageGroupObjectInternal(client, key, cid, UploadProcess.SubObjectIds, attributes);
            if (OnPutObjectInternal(client, obj, session))
            {
                UploadProcess.ObjectId = obj.ObjectId.String();
                OnWriteFileInternal(new FileInfo(filePath).Directory.FullName + DirectorySeparatorChar + $"{obj.ObjectId.String()}_{timeStamp}.seed", Utility.StrictUTF8.GetBytes($"{cid.String()}_{obj.ObjectId.String()}"));
                UploadProcess.TimeSpent = stopWatch.Elapsed;
                Console.WriteLine("Fs file upload successfully");
                Console.WriteLine($"Fs upload info:{UploadProcess.ToJson()}");
                return;
            }
        }

        /// <summary>
        /// User can invoke this command to download a bigfile.
        /// </summary>
        /// <param name="containerId">containerId</param>
        /// <param name="objectId">objectId</param>
        /// <param name="filePath">download path</param>
        /// <param name="paddress">account address(The first account of the wallet,default)</param>
        /// <param name="again">whether to recover the last task</param>
        [ConsoleCommand("fs file download", Category = "FileStorageService", Description = "Download file")]
        private void OnDownloadFile(string containerId, string objectId, string filePath, string paddress = null, bool again = false)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            if (!CheckAndParseAccount(paddress, out UInt160 account, out ECDsa key)) return;
            if (!ParseContainerID(containerId, out var cid)) return;
            if (!ParseObjectID(objectId, out var oid)) return;
            using var client = OnCreateClientInternal(key);
            if (client is null) return;
            var totalDataSize = 0ul;
            string timestamp = null;
            string FileName = null;
            if (DownloadProcess is null || !again)
            {
                timestamp = DateTimeOffset.Now.ToUnixTimeSeconds().ToString();
                var subObjectIDs = new List<ObjectID>();
                Neo.FileStorage.API.Object.Object obj = OnGetObjectInternal(client, cid, oid);
                if (obj is null || obj.ObjectType != ObjectType.StorageGroup) throw new Exception($"Fs can not find file index, please provide the correct objectid");
                var sg = StorageGroup.Parser.ParseFrom(obj.Payload.ToByteArray());
                totalDataSize = sg.ValidationDataSize;
                Console.WriteLine($"Download file index successfully");
                Console.WriteLine($"File objects size: {totalDataSize}");
                Console.WriteLine($"File subobject list:");
                foreach (var m in sg.Members)
                {
                    subObjectIDs.Add(m);
                    Console.WriteLine($"subobjectId:{m.String()}");
                }
                obj.Attributes.ForEach(p =>
                {
                    if (p.Key == Header.Types.Attribute.AttributeFileName) FileName = p.Value;
                });
                DownloadProcess = new Process(containerId, objectId, FileName, filePath, totalDataSize, timestamp, subObjectIDs.Count);
                DownloadProcess.SubObjectIds = subObjectIDs.ToArray();
            }
            else
            {
                if (DownloadProcess.ContainerId != containerId || DownloadProcess.ObjectId != objectId) throw new Exception($"The new task doesn't match with the last failed one");
                DownloadProcess.Rest();
                totalDataSize = DownloadProcess.Total;
                timestamp = CommandsPlugin.DownloadProcess.TimeStamp;
            }
            DirectoryInfo parentDirectory = null;
            if (!Directory.Exists(filePath)) parentDirectory = Directory.CreateDirectory(filePath);
            else parentDirectory = new DirectoryInfo(filePath);
            var childrenDirectorys = parentDirectory.GetDirectories().Where(p => p.Name == DownloadProcess.TimeStamp).ToList();
            DirectoryInfo workDirectory = null;
            if (childrenDirectorys.Count > 0)
            {
                workDirectory = childrenDirectorys.First();
            }
            else
            {
                workDirectory = parentDirectory.CreateSubdirectory(DownloadProcess.TimeStamp);
            }
            var taskCounts = 10;
            var tasks = new Task[taskCounts];
            for (int index = 0; index < taskCounts; index++)
            {
                var threadIndex = index;
                var task = new Task(() =>
                {
                    try
                    {
                        using var internalClient = OnCreateClientInternal(key);
                        if (internalClient is null) return;
                        int i = 0;
                        while (threadIndex + i * taskCounts < DownloadProcess.SubObjectIds.Length)
                        {
                            var oid = DownloadProcess.SubObjectIds[threadIndex + i * taskCounts];
                            string tempfilepath = workDirectory.FullName + DirectorySeparatorChar + "QS_" + DownloadProcess.SubObjectIds[threadIndex + i * taskCounts].String();
                            FileInfo tempfile = new FileInfo(tempfilepath);
                            if (tempfile.Exists)
                            {
                                using FileStream tempreadstream = new FileStream(tempfilepath, FileMode.Open);
                                byte[] downedData = new byte[tempreadstream.Length];
                                tempreadstream.Read(downedData, 0, downedData.Length);
                                var objheader = OnGetObjectHeaderInternal(internalClient, cid, oid);
                                if (objheader is null) return;
                                if (downedData.Sha256().SequenceEqual(objheader.PayloadChecksum.Sum.ToByteArray()))
                                {
                                    Console.WriteLine($"Download subobject successfully,objectId:{oid.String()},degree of completion:{Interlocked.Add(ref DownloadProcess.Current, (ulong)downedData.Length)}/{totalDataSize}");
                                    i++;
                                    continue;
                                }
                                else
                                {
                                    tempreadstream.Dispose();
                                    tempfile.Delete();
                                }
                            }
                            using FileStream tempstream = new FileStream(tempfilepath, FileMode.Create, FileAccess.Write, FileShare.Write);
                            var obj = OnGetObjectInternal(internalClient, cid, oid);
                            if (obj is null) return;
                            var payload = obj.Payload.ToByteArray();
                            tempstream.Write(payload, 0, payload.Length);
                            tempstream.Flush();
                            tempstream.Close();
                            tempstream.Dispose();
                            Console.WriteLine($"Download subobject successfully,objectId:{oid.String()},degree of completion:{Interlocked.Add(ref DownloadProcess.Current, (ulong)payload.Length)}/{totalDataSize}");
                            i++;
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Fs download task fault,threadIndex:{threadIndex},error:{e}");
                    }
                });
                tasks[index] = task;
                task.Start();
            }
            Task.WaitAll(tasks);
            //check failed task
            List<ObjectID> Comparefiles = new List<ObjectID>();
            for (int i = 0; i < DownloadProcess.SubObjectIds.Length; i++)
            {
                bool hasfile = false;
                foreach (FileInfo Tempfile in workDirectory.GetFiles())
                {
                    if (Tempfile.Name.Split('_')[1] == DownloadProcess.SubObjectIds[i].String())
                    {
                        hasfile = true;
                        break;
                    }
                }
                if (hasfile == false)
                {
                    Comparefiles.Add(DownloadProcess.SubObjectIds[i]);
                }
            }
            if (Comparefiles.Count > 0)
            {
                Console.WriteLine($"Fs download data is missing, please download again");
                return;
            }
            //write file
            string downPath = workDirectory.FullName + DirectorySeparatorChar + DownloadProcess.FileName;
            using (FileStream writestream = new FileStream(downPath, FileMode.Create, FileAccess.Write, FileShare.Write))
            {
                for (int index = 0; index < DownloadProcess.SubObjectIds.Length; index++)
                {
                    string tempfilepath = workDirectory.FullName + DirectorySeparatorChar + "QS_" + DownloadProcess.SubObjectIds[index].String();
                    FileInfo Tempfile = new FileInfo(tempfilepath);
                    using FileStream readTempStream = new FileStream(Tempfile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    long onefileLength = Tempfile.Length;
                    byte[] buffer = new byte[Convert.ToInt32(onefileLength)];
                    readTempStream.Read(buffer, 0, Convert.ToInt32(onefileLength));
                    writestream.Write(buffer, 0, Convert.ToInt32(onefileLength));
                    readTempStream.Dispose();
                }
                writestream.Flush();
                writestream.Close();
                writestream.Dispose();
            }
            //delete temp file
            workDirectory.GetFiles().Where(p => p.Name != DownloadProcess.FileName).ToList().ForEach(p => p.Delete());
            Console.WriteLine("Download file successfully");
            DownloadProcess.TimeSpent = stopWatch.Elapsed;
        }

        /// <summary>
        /// User can invoke this command to download a bigfile by seed file.
        /// </summary>
        /// <param name="filePath">seed file path</param>
        /// <param name="paddress">account address(The first account of the wallet,default)</param>
        /// <param name="again">whether to recover the last task</param>
        [ConsoleCommand("fs file fastdownload", Category = "FileStorageService", Description = "Download file")]
        private void OnDownloadFileBySeed(string filePath, string paddress = null, bool again = false)
        {
            FileInfo file = new(filePath);
            if (!file.Exists) throw new Exception($"The specified file does not exist");
            var seed = Utility.StrictUTF8.GetString(OnGetFileInternal(filePath, 0, (int)file.Length, file.Length));
            OnDownloadFile(seed.Split("_")[0], seed.Split("_")[1], file.Directory.FullName, paddress, again);
        }

        private byte[] OnGetFileInternal(string filePath, long start, int length, long totalLength)
        {
            using FileStream ServerStream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1024 * 80, true);
            byte[] buffer;
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

        private void OnWriteFileInternal(string filePath, byte[] data)
        {
            using (FileStream writestream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Write))
            {
                writestream.Write(data, 0, data.Length);
                writestream.Flush();
                writestream.Close();
                writestream.Dispose();
            }
        }

        private class Process
        {
            public string ContainerId;
            public string ObjectId;
            public string FileName;
            public string FilePath;
            public string TimeStamp;
            public ulong Current;
            public ulong Total;
            public ObjectID[] SubObjectIds;
            public TimeSpan TimeSpent;

            public Process(string cid, string oid, string fileName, string filePath, ulong total, string timestamp, long count)
            {
                ContainerId = cid;
                ObjectId = oid;
                FileName = fileName;
                FilePath = filePath;
                Current = 0;
                Total = total;
                TimeStamp = timestamp;
                SubObjectIds = new ObjectID[count];
            }

            public void Rest()
            {
                Current = 0;
            }

            public ulong Add(ulong completed)
            {
                return Interlocked.Add(ref Current, completed);
            }

            public JObject ToJson()
            {
                return new JObject
                {
                    ["containerId"] = ContainerId,
                    ["objectId"] = ObjectId,
                    ["fileName"] = FileName,
                    ["filePath"] = FilePath,
                    ["timeStamp"] = TimeStamp,
                    ["current"] = Current,
                    ["total"] = Total,
                    ["finish"] = TimeSpent.ToString()
                };
            }
        }
    }
}
