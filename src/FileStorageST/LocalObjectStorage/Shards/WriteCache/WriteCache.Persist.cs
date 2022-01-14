using System;
using System.Linq;

namespace Neo.FileStorage.Storage.LocalObjectStorage.Shards
{
    public sealed partial class WriteCache : IDisposable
    {
        private void Persist(object _1)
        {
            if (mem.IsEmpty) return;
            var m = mem.Values.OrderBy(p => p.SAddress).ToArray();
            foreach (var obj in m)
            {
                mem.TryRemove(obj.Address, out var _2);
                lock (memorySizeLocker)
                {
                    memorySize -= (ulong)obj.Data.Length;
                }
                Utility.Log(nameof(WriteCache), LogLevel.Debug, $"in-mem DELETE pesist, address={obj.SAddress}");
            }
            PersistSmallObjects(m);
            if (m.Length > 0)
                Utility.Log(nameof(WriteCache), LogLevel.Debug, $"persissted items to disk, total={m.Length}");
        }

        private void PersistSmallObjects(params ObjectInfo[] objs)
        {
            var cacheSize = EstimateCacheSize();
            var overflowIndex = objs.Length;
            for (int i = 0; i < objs.Length; i++)
            {
                var newSize = cacheSize + SmallObjectSize;
                if (MaxCacheSize < newSize)
                {
                    overflowIndex = i;
                    break;
                }
                cacheSize = newSize;
                db.Put(objs[i]);
            }
            EvictObjects(overflowIndex);
            for (int i = 0; i < overflowIndex; i++)
            {
                objCounters.IncSmallCount();
                Utility.Log(nameof(WriteCache), LogLevel.Debug, $"db PUT, address={objs[i].SAddress}");
            }
            for (int i = overflowIndex; i < objs.Length; i++)
                flushed.Add(objs[i].Address, true);
            AddToFlushQueue(objs, overflowIndex);
        }

        private void PersistBigObject(ObjectInfo obj)
        {
            var cacheSize = EstimateCacheSize();
            var metaIndex = 0;
            if (cacheSize + MaxObjectSize <= MaxCacheSize)
            {
                fsTree.Put(obj.Object.Address, obj.Data);
                objCounters.IncBigCount();
                Utility.Log(nameof(WriteCache), LogLevel.Debug, $"fstree PUT, address={obj.SAddress}");
                metaIndex = 1;
            }
            AddToFlushQueue(new[] { obj }, metaIndex);
        }

        private void AddToFlushQueue(ObjectInfo[] objs, int metaIndex)
        {
            for (int i = 0; i < objs.Length; i++)
                flushQueue.Enqueue((objs[i], i < metaIndex));
        }
    }
}
