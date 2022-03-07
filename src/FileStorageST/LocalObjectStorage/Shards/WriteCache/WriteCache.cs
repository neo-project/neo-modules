using Google.Protobuf;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Cache;
using Neo.FileStorage.Storage.LocalObjectStorage.Blobstor;
using Neo.FileStorage.Storage.LocalObjectStorage.Metabase;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace Neo.FileStorage.Storage.LocalObjectStorage.Shards
{
    public sealed partial class WriteCache : IDisposable
    {
        public const string DefaultDirName = "Cache";
        public const string DBDirName = "DB";
        public const string FileTreeDirName = "FSTree";
        public const int LRUKeysCount = 256 * 1024 * 8;
        public const int DefaultInterval = 1000;
        public const int FlushBatchSize = 512;
        public const ulong DefaultMaxCacheSize = 1ul << 30;
        public const ulong DefaultMemorySize = 1ul << 30;
        public const ulong DefaultMaxObjectSize = 64ul << 20;
        public const ulong DefaultSmallObjectSize = 32ul << 10;

        private readonly BlobStorage blobStorage;
        private readonly MB metabase;
        private WriteCacheSettings settings;
        private readonly string rootPath;
        private ulong MaxCacheSize => settings.MaxCacheSize;
        private ulong MaxMemorySize => settings.MaxMemorySize;
        private ulong MaxObjectSize => settings.MaxObjectSize;
        private ulong SmallObjectSize => settings.SmallObjectSize;
        private ulong memorySize = 0;
        private readonly object memorySizeLocker = new(), dbSizeLocker = new();
        private readonly ConcurrentDictionary<Address, ObjectInfo> mem = new();
        private readonly FSTree fsTree;
        private CacheDB db;
        private readonly LRUCache<Address, bool> flushed;
        private Timer persistTimer;
        private Timer flushTimer;
        private Timer queueTimer;
        private ObjectCounters objCounters;
        private readonly ConcurrentQueue<(ObjectInfo, bool)> flushQueue = new();
        private readonly ConcurrentDictionary<Address, bool> needCompress = new();

        public WriteCache(string path, WriteCacheSettings settings, BlobStorage blobStor, MB mb)
        {
            this.settings = settings;
            rootPath = path;
            blobStorage = blobStor;
            metabase = mb;
            fsTree = new(Path.Join(rootPath, FileTreeDirName), 1, 1);
            flushed = new(LRUKeysCount);
        }

        public void Open()
        {
            var full = Path.GetFullPath(Path.Join(rootPath, DBDirName));
            if (!Directory.Exists(full))
                Directory.CreateDirectory(full);
            db = new CacheDB(full);
            persistTimer = new(Persist, null, DefaultInterval, DefaultInterval);
            flushTimer = new(Flush, null, DefaultInterval, DefaultInterval);
            queueTimer = new(FlushQueue, null, DefaultInterval, DefaultInterval);
            objCounters = new(db, fsTree);
            objCounters.Load();
        }

        private void Flush(object _)
        {
            FlushSmallObjects();
            FlushBigObjects();
        }

        private void EvictObjects(int count)
        {
            int sum = flushed.Count + count;
            if (sum < LRUKeysCount) return;
            List<Address> dbKeys = new();
            List<Address> diskKeys = new();
            for (int i = 0; i < LRUKeysCount - sum; i++)
            {
                if (!flushed.RemoveOldest(out (Address, bool) removed))
                    break;
                if (removed.Item2)
                    dbKeys.Add(removed.Item1);
                else
                    diskKeys.Add(removed.Item1);
            }

            foreach (var address in dbKeys)
            {
                var length = db.Get(address)?.Length ?? 0;
                if (0 < length)
                {
                    db.Delete(address);
                    objCounters.DecSmallCount();
                    Utility.Log(nameof(WriteCache), LogLevel.Debug, $"db DELETE, address={address.String()}");
                }
            }
            foreach (var address in diskKeys)
            {
                try
                {
                    fsTree.Delete(address);
                    objCounters.DecBigCount();
                    Utility.Log(nameof(WriteCache), LogLevel.Debug, $"fs tree DELETE, address={address.String()}");
                }
                catch (Exception e)
                {
                    Utility.Log(nameof(WriteCache), LogLevel.Error, $"can't remove from fstree, address={address.String()}, error={e.Message}");
                }
            }
        }

        public void Dispose()
        {
            persistTimer?.Dispose();
            flushTimer?.Dispose();
            queueTimer?.Dispose();
            db?.Dispose();
            flushed?.Purge();
        }
    }
}
