using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Cache;
using Neo.FileStorage.Database;
using Neo.FileStorage.Database.LevelDB;
using Neo.FileStorage.Storage.LocalObjectStorage.Blobstor;
using Neo.FileStorage.Storage.LocalObjectStorage.Metabase;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.LocalObjectStorage.Shards
{
    public sealed partial class WriteCache : IDisposable
    {
        private class ObjectInfo
        {
            public FSObject Object;
            public string SAddress;
            public byte[] Data;
        }

        public const string DBName = "Data_Cache_Small";
        public const string FileTreeDirName = "FileTree_Cache";
        public const int LRUKeysCount = 256 * 1024 * 8;
        public const int DefaultInterval = 1000;
        public const int FlushBatchSize = 512;
        public const ulong DefaultMaxCacheSize = 1ul << 30;
        public const ulong DefaultMemorySize = 1ul << 30;
        public const ulong DefaultMaxObjectSize = 64ul << 20;
        public const ulong DefaultSmallObjectSize = 32ul << 10;
        private readonly string path;
        private readonly BlobStorage blobStorage;
        private readonly MB metabase;
        private WriteCacheSettings settings;
        private ulong MaxCacheSize => settings.MaxCacheSize;
        private ulong MaxMemorySize => settings.MaxMemorySize;
        private ulong MaxObjectSize => settings.MaxObjectSize;
        private ulong SmallObjectSize => settings.SmallObjectSize;
        private ulong memorySize = 0;
        private readonly object memorySizeLocker = new(), dbSizeLocker = new();
        private readonly ConcurrentDictionary<string, ObjectInfo> mem = new();
        private readonly FSTree fsTree;
        private IDB db;
        private readonly LRUCache<string, bool> flushed;
        private Timer persistTimer;
        private Timer flushQueueTimer;
        private ObjectCounters objCounters;
        private readonly ConcurrentQueue<(ObjectInfo, bool)> flushQueue = new();

        public WriteCache(WriteCacheSettings settings, BlobStorage blobStor, MB mb)
        {
            path = settings.Path;
            this.settings = settings;
            blobStorage = blobStor;
            metabase = mb;
            fsTree = new(Path.Join(path, FileTreeDirName), 1, 1);
            flushed = new(LRUKeysCount);
        }

        public void Open()
        {
            var full = Path.GetFullPath(Path.Join(path, DBName));
            if (!Directory.Exists(full))
                Directory.CreateDirectory(full);
            db = new DB(full);
            persistTimer = new(OnPersist, null, DefaultInterval, DefaultInterval);
            flushQueueTimer = new(OnFlushQueue, null, DefaultInterval, DefaultInterval / 2);
            objCounters = new(db, fsTree);
            objCounters.Load();
        }

        private void OnPersist(object _)
        {
            Persist();
            FlushSmallObjects();
            FlushBigObjects();
        }

        private void OnFlushQueue(object _)
        {
            FlushQueue();
        }

        private void EvictObjects(int count)
        {
            int sum = flushed.Count + count;
            if (sum < LRUKeysCount) return;
            List<string> dbKeys = new();
            List<string> diskKeys = new();
            for (int i = 0; i < LRUKeysCount - sum; i++)
            {
                if (!flushed.RemoveOldest(out (string, bool) removed))
                    break;
                if (removed.Item2)
                    dbKeys.Add(removed.Item1);
                else
                    diskKeys.Add(removed.Item1);
            }

            foreach (var saddress in dbKeys)
            {
                var length = db.Get(Utility.StrictUTF8.GetBytes(saddress))?.Length ?? 0;
                if (0 < length)
                {
                    db.Delete(Utility.StrictUTF8.GetBytes(saddress));
                    objCounters.DecSmallCount();
                    Utility.Log(nameof(WriteCache), LogLevel.Debug, $"db DELETE, address={saddress}");
                }
            }
            foreach (var saddress in diskKeys)
            {
                var address = Address.ParseString(saddress);
                try
                {
                    fsTree.Delete(address);
                    objCounters.DecBigCount();
                    Utility.Log(nameof(WriteCache), LogLevel.Debug, $"fs tree DELETE, address={saddress}");
                }
                catch (Exception e)
                {
                    Utility.Log(nameof(WriteCache), LogLevel.Error, $"can't remove from fstree, address={saddress}, error={e.Message}");
                }
            }
        }

        public void Dispose()
        {
            persistTimer?.Dispose();
            flushQueueTimer?.Dispose();
            db?.Dispose();
            flushed?.Purge();
        }
    }
}
