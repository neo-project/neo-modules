using Neo.FileStorage.Storage.LocalObjectStorage.Blob;
using System;
using System.Collections.Generic;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.LocalObjectStorage.Shards
{
    public sealed partial class WriteCache : IDisposable
    {
        private void FlushSmallObjects()
        {
            int i = 0;
            List<ObjectInfo> toFlush = new();
            db.Iterate(Array.Empty<byte>(), (key, value) =>
             {
                 ObjectInfo oi = new()
                 {
                     Object = FSObject.Parser.ParseFrom(value),
                     SAddress = Utility.StrictUTF8.GetString(key),
                 };
                 if (flushed.TryPeek(oi.SAddress, out _))
                     return false;
                 toFlush.Add(oi);
                 i++;
                 if (FlushBatchSize <= i) return true;
                 return false;
             });
            AddToFlushQueue(toFlush.ToArray(), 0);
            EvictObjects(toFlush.Count);
            foreach (var oi in toFlush)
                flushed.Add(oi.SAddress, true);
            if (toFlush.Count > 0)
                Utility.Log(nameof(WriteCache), LogLevel.Debug, $"flushed items from write cache, count={toFlush.Count}");
        }

        private void FlushBigObjects()
        {
            try
            {
                fsTree.Iterate((address, data) =>
                {
                    if (flushed.TryPeek(address.String(), out _)) return;
                    blobStorage.PutRaw(address, data);
                    flushed.Add(address.String(), false);
                });
            }
            catch (Exception e)
            {
                Utility.Log(nameof(WriteCache), LogLevel.Warning, $"could not flush big objects, error={e.Message}");
            }
        }

        private void FlushQueue()
        {
            while (flushQueue.TryDequeue(out var item))
            {
                var (oi, metaOnly) = item;
                WriteObject(oi.Object, metaOnly);
            }
        }

        private void WriteObject(FSObject obj, bool metaOnly)
        {
            try
            {
                BlobovniczaID id = null;
                if (!metaOnly)
                {
                    id = blobStorage.Put(obj);
                }
                metabase.Put(obj, id);
            }
            catch (Exception e)
            {
                Utility.Log(nameof(WriteCache), LogLevel.Error, $"can't write object to main storage, error={e.Message}");
            }
        }
    }
}
