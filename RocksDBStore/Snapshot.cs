using Neo.Persistence;
using RocksDbSharp;
using System;
using System.Collections.Generic;

namespace Neo.Plugins.Storage
{
    internal class Snapshot : ISnapshot
    {
        private readonly Store store;
        private readonly RocksDb db;
        private readonly RocksDbSharp.Snapshot snapshot;
        private readonly WriteBatch batch;
        private readonly ReadOptions options;

        public Snapshot(Store store, RocksDb db)
        {
            this.store = store;
            this.db = db;
            this.snapshot = db.CreateSnapshot();
            this.batch = new WriteBatch();

            options = new ReadOptions();
            options.SetFillCache(false);
            options.SetSnapshot(snapshot);
        }

        public void Commit()
        {
            db.Write(batch, Options.WriteDefault);
        }

        public void Delete(byte table, byte[] key)
        {
            batch.Delete(key, store.GetFamily(table));
        }

        public void Put(byte table, byte[] key, byte[] value)
        {
            batch.Put(key, value, store.GetFamily(table));
        }

        public IEnumerable<(byte[] Key, byte[] Value)> Find(byte table, byte[] prefix)
        {
            using var it = db.NewIterator(store.GetFamily(table), options);
            for (it.Seek(prefix); it.Valid(); it.Next())
            {
                var key = it.Key();
                byte[] y = prefix;
                if (key.Length < y.Length) break;
                if (!key.AsSpan().StartsWith(y)) break;
                yield return (key, it.Value());
            }
        }

        public byte[] TryGet(byte table, byte[] key)
        {
            return db.Get(key, store.GetFamily(table), options);
        }

        public void Dispose()
        {
            snapshot.Dispose();
            batch.Dispose();
        }
    }
}
