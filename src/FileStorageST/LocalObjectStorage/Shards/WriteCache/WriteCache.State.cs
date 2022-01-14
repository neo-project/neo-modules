using Neo.FileStorage.Database;
using Neo.FileStorage.Storage.LocalObjectStorage.Blobstor;
using System;
using System.Threading;

namespace Neo.FileStorage.Storage.LocalObjectStorage.Shards
{
    public sealed partial class WriteCache : IDisposable
    {
        private class ObjectCounters
        {
            public ulong SmallCount;
            public ulong BigCount;
            private readonly FSTree fsTree;
            private readonly CacheDB db;

            public ObjectCounters(CacheDB db, FSTree fsTree)
            {
                this.db = db;
                this.fsTree = fsTree;
            }

            public void Load()
            {
                db.Iterate(data =>
                {
                    SmallCount++;
                    return false;
                });
                try
                {
                    fsTree.Iterate((address, data) =>
                    {
                        BigCount++;
                    });
                }
                catch (Exception e)
                {
                    Utility.Log(nameof(WriteCache), LogLevel.Warning, $"could not load big objects count, error={e.Message}");
                }
            }

            public void IncSmallCount()
            {
                Interlocked.Increment(ref SmallCount);
            }

            public void DecSmallCount()
            {
                Interlocked.Decrement(ref SmallCount);
            }

            public void IncBigCount()
            {
                Interlocked.Increment(ref BigCount);
            }

            public void DecBigCount()
            {
                Interlocked.Decrement(ref BigCount);
            }
        }

        private ulong EstimateCacheSize()
        {
            return objCounters.SmallCount * SmallObjectSize + objCounters.BigCount * MaxObjectSize;
        }
    }
}
