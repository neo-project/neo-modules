using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Protobuf;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Cache;
using Neo.FileStorage.LocalObjectStorage.Blob;
using Neo.FileStorage.LocalObjectStorage.Blobstor;
using Neo.FileStorage.LocalObjectStorage.MetaBase;
using Neo.IO.Data.LevelDB;
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
    public sealed class writeCache : IDisposable
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
        public string Path { get; init; }
        public Blobstorage Blobstorage { get; init; }
        public MB Mb { get; init; }
        public int MaxMemorySize { get; init; }
        public int MaxDBSize { get; init; }
        public int MaxObjectSize { get; init; }
        public int SmallObjectSize { get; init; }
        public int FlushWorkerCount { get; init; }
        private int currentMemorySize = 0;
        private readonly ConcurrentDictionary<string, FSObject> mem = new();
        private FSTree fsTree;
        private DB db;
        private LRUCache<string, bool> flushed;
        public void Open()
        {
            var full = System.IO.Path.GetFullPath(System.IO.Path.Join(Path, DBName));
            if (!Directory.Exists(full))
                Directory.CreateDirectory(full);
            db = DB.Open(full, new Options { CreateIfMissing = true, FilterPolicy = Native.leveldb_filterpolicy_create_bloom(15) });
            fsTree = new()
            {
                RootPath = System.IO.Path.Join(Path, FileTreeDirName),
                Depth = 1,
                DirNameLen = 1,
            };
            flushed = new(LRUKeysCount);
        }

        public void Dispose()
        {
            db?.Dispose();
            flushed.Purge();
        }

        public FSObject Get(Address address)
        {
            string saddress = address.String();
            if (mem.TryGetValue(saddress, out FSObject obj))
            {
                return obj;
            }
            byte[] data = db.Get(ReadOptions.Default, Utility.StrictUTF8.GetBytes(address.String()));
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
            int size = obj.CalculateSize();
            if (MaxObjectSize < size)
                throw new InvalidOperationException("Object too big");
            if (size < SmallObjectSize && currentMemorySize + size <= MaxMemorySize)
            {
                currentMemorySize += size;
                mem[obj.Address.String()] = obj;
                return;
            }
            PersistObjects(obj);
        }

        private void PersistObjects(params FSObject[] objs)
        {
            var toDisk = PersistCache(objs);
            foreach (var obj in objs)
            {
                if (toDisk.Contains(obj))
                    WriteObject(obj, false);
                else
                    WriteObject(obj, true);
            }
        }

        private List<FSObject> PersistCache(FSObject[] objs)
        {
            List<FSObject> fails = new();
            List<FSObject> dones = new();
            foreach (var obj in objs)
            {
                int size = obj.CalculateSize();
                if (SmallObjectSize <= size)
                {
                    fails.Add(obj);
                    continue;
                }
                db.Put(WriteOptions.Default, Utility.StrictUTF8.GetBytes(obj.Address.String()), obj.ToByteArray());
                dones.Add(obj);
            }
            if (dones.Any())
            {
                EvictObjects(dones.Count);
                foreach (var obj in dones)
                {
                    flushed.TryAdd(obj.Address.String(), true);
                }
            }
            List<FSObject> failDisks = new();
            foreach (var obj in fails)
            {
                var size = obj.CalculateSize();
                if (MaxObjectSize < size)
                {
                    failDisks.Add(obj);
                    continue;
                }
                fsTree.Put(obj.Address, obj.ToByteArray());
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
                db.Delete(WriteOptions.Default, Utility.StrictUTF8.GetBytes(saddress));
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

        public void Delete(FSObject obj)
        {
            string saddress = obj.Address.String();
            if (mem.TryRemove(saddress, out _))
            {
                return;
            }
            if (db.Contains(ReadOptions.Default, Utility.StrictUTF8.GetBytes(saddress)))
            {
                db.Delete(WriteOptions.Default, Utility.StrictUTF8.GetBytes(saddress));
                return;
            }
            fsTree.Delete(obj.Address);
        }
    }
}
