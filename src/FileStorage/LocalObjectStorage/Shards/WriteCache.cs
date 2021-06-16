using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Timers;
using Google.Protobuf;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Cache;
using Neo.FileStorage.Database;
using Neo.FileStorage.Database.LevelDB;
using Neo.FileStorage.LocalObjectStorage.Blob;
using Neo.FileStorage.LocalObjectStorage.Blobstor;
using Neo.FileStorage.LocalObjectStorage.Metabase;
using Neo.Persistence;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.LocalObjectStorage.Shards
{
    /// <summary>
    /// Package writecache implements write-cache for objects.
    ///
    /// It contains in-memory cache of fixed size and underlying database
    /// (usually on SSD) for storing small objects.
    /// There are 3 places where object can be:
    /// 1. In-memory cache.
    /// 2. On-disk cache DB.
    /// 3. Main storage (blobstor).
    ///
    /// There are 2 types of background jobs:
    /// 1. Persisting objects from in-memory cache to database.
    /// 2. Flushing objects from database to blobstor.
    ///	  On flushing object address is put in in-memory LRU cache.
    ///	  The actual deletion from the DB is done when object
    ///	  is evicted from this cache.
    ///
    /// Putting objects to the main storage is done by multiple workers.
    /// Some of them prioritize flushing items, others prioritize putting new objects.
    /// The current ration is 50/50. This helps to make some progress even under load.
    /// </summary>
    public sealed class WriteCache : IDisposable
    {
        private class ObjectInfo
        {
            public FSObject Object;
            public string SAddress;
            public byte[] Data;
        }

        public const string DBName = "Data_CacheSmall";
        public const string FileTreeDirName = "FileTree_Cache";
        public const int LRUKeysCount = 256 * 1024 * 8;
        public const int DefaultInterval = 1000;
        public const int FlushBatchSize = 512;
        public const int DefaultMemorySize = 1 << 30;
        public const int DefaultMaxObjectSize = 64 << 20;
        public const int DefaultSmallObjectSize = 32 << 10;
        public string Path { get; init; }
        public BlobStorage Blobstorage { get; init; }
        public MB Mb { get; init; }
        public int MaxMemorySize { get; init; }
        public int MaxDBSize { get; init; }
        public int MaxObjectSize { get; init; }
        public int SmallObjectSize { get; init; }
        public int FlushWorkerCount { get; init; }
        private int currentMemorySize = 0;
        private readonly ConcurrentDictionary<string, ObjectInfo> mem = new();
        private FSTree fsTree;
        private IDB db;
        private LRUCache<string, bool> flushed;
        private readonly Timer timer = new(DefaultInterval);

        public void Open()
        {
            var full = System.IO.Path.GetFullPath(System.IO.Path.Join(Path, DBName));
            if (!Directory.Exists(full))
                Directory.CreateDirectory(full);
            db = new DB(full);
            fsTree = new()
            {
                RootPath = System.IO.Path.Join(Path, FileTreeDirName),
                Depth = 1,
                DirNameLen = 1,
            };
            flushed = new(LRUKeysCount);
            SetTimer();
        }

        private void SetTimer()
        {
            timer.Elapsed += OnTimer;
            timer.AutoReset = true;
            timer.Start();
        }

        private void OnTimer(object source, ElapsedEventArgs args)
        {
            Persist();
            Flush();
        }

        private void Persist()
        {
            var m = mem.Values.OrderBy(p => p.SAddress);
            mem.Clear();
            PersistObjects(m.ToArray());
            foreach (var oi in m)
            {
                currentMemorySize -= oi.Data.Length;
            }
        }

        private void Flush()
        {
            int i = 0;
            List<ObjectInfo> m = new();
            db.Iterate(Array.Empty<byte>(), (key, value) =>
             {
                 ObjectInfo oi = new()
                 {
                     Object = FSObject.Parser.ParseFrom(value),
                     SAddress = Utility.StrictUTF8.GetString(key),
                 };
                 if (flushed.TryPeek(oi.SAddress, out _))
                     return false;
                 m.Add(oi);
                 WriteObject(oi.Object, false);
                 i++;
                 if (FlushBatchSize <= i) return true;
                 return false;
             });
            EvictObjects(m.Count);
            foreach (var oi in m)
                flushed.TryAdd(oi.SAddress, true);
        }

        public void Dispose()
        {
            timer.Stop();
            timer.Dispose();
            db?.Dispose();
            flushed.Purge();
        }

        public FSObject Get(Address address)
        {
            string saddress = address.String();
            if (mem.TryGetValue(saddress, out ObjectInfo oi))
            {
                return oi.Object;
            }
            byte[] data = db.Get(Utility.StrictUTF8.GetBytes(address.String()));
            if (data is not null)
            {
                flushed.TryGet(saddress, out _);
                return FSObject.Parser.ParseFrom(data);
            }
            data = fsTree.Get(address);
            flushed.TryGet(saddress, out _);
            return FSObject.Parser.ParseFrom(data);
        }

        public void Put(FSObject obj)
        {
            ObjectInfo oi = new()
            {
                Object = obj,
                SAddress = obj.Address.String(),
                Data = obj.ToByteArray()
            };
            if (MaxObjectSize < oi.Data.Length)
                throw new InvalidOperationException("Object too big");
            if (oi.Data.Length < SmallObjectSize && currentMemorySize + oi.Data.Length <= MaxMemorySize)
            {
                currentMemorySize += oi.Data.Length;
                mem[obj.Address.String()] = oi;
                return;
            }
            PersistObjects(oi);
        }

        private void PersistObjects(params ObjectInfo[] ois)
        {
            var failDisk = PersistCache(ois);
            foreach (var oi in ois)
            {
                if (failDisk.Contains(oi))
                    WriteObject(oi.Object, false);
                else
                    WriteObject(oi.Object, true);
            }
        }

        private List<ObjectInfo> PersistCache(ObjectInfo[] ois)
        {
            List<ObjectInfo> fails = new();
            List<ObjectInfo> dones = new();
            foreach (var oi in ois)
            {
                if (SmallObjectSize <= oi.Data.Length)
                {
                    fails.Add(oi);
                    continue;
                }
                db.Put(Utility.StrictUTF8.GetBytes(oi.SAddress), oi.Data);
                dones.Add(oi);
            }
            if (dones.Any())
            {
                EvictObjects(dones.Count);
                foreach (var oi in dones)
                {
                    flushed.TryAdd(oi.SAddress, true);
                }
            }
            List<ObjectInfo> failDisks = new();
            foreach (var oi in fails)
            {
                if (MaxObjectSize < oi.Data.Length)
                {
                    failDisks.Add(oi);
                    continue;
                }
                fsTree.Put(oi.Object.Address, oi.Data);
            }
            return failDisks;
        }

        private void EvictObjects(int count)
        {
            int sum = flushed.Count + count;
            if (sum < LRUKeysCount) return;
            List<string> memKeys = new();
            List<string> diskKeys = new();
            for (int i = 0; i < LRUKeysCount - sum; i++)
            {
                if (!flushed.RemoveOldest(out (string, bool) removed))
                    break;
                if (removed.Item2)
                    memKeys.Add(removed.Item1);
                else
                    diskKeys.Add(removed.Item1);
            }
            foreach (var saddress in memKeys)
            {
                db.Delete(Utility.StrictUTF8.GetBytes(saddress));
            }
            foreach (var saddress in diskKeys)
            {
                var address = Address.ParseString(saddress);
                fsTree.Delete(address);
            }
        }

        private void WriteObject(FSObject obj, bool metaOnly)
        {
            BlobovniczaID id = null;
            if (metaOnly)
            {
                id = Blobstorage.Put(obj);
            }
            Mb.Put(obj, id);
        }

        public void Delete(Address address)
        {
            string saddress = address.String();
            byte[] key = Utility.StrictUTF8.GetBytes(saddress);
            if (mem.TryRemove(saddress, out _))
            {
                return;
            }
            if (db.Contains(key))
            {
                db.Delete(key);
                return;
            }
            fsTree.Delete(address);
        }
    }
}
