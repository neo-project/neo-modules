using Neo.IO.Caching;
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
            batch.Delete(key ?? Array.Empty<byte>(), store.GetFamily(table));
        }

        public void Put(byte table, byte[] key, byte[] value)
        {
            batch.Put(key ?? Array.Empty<byte>(), value, store.GetFamily(table));
        }

        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte table, byte[] keyOrPrefix, SeekDirection direction)
        {
            if (keyOrPrefix == null) keyOrPrefix = Array.Empty<byte>();

            using var it = db.NewIterator(store.GetFamily(table), options);

            if (direction == SeekDirection.Forward)
                for (it.Seek(keyOrPrefix); it.Valid(); it.Next())
                    yield return (it.Key(), it.Value());
            else
                for (it.SeekForPrev(keyOrPrefix); it.Valid(); it.Prev())
                    yield return (it.Key(), it.Value());
        }

        public bool Contains(byte table, byte[] key)
        {
            return db.Get(key ?? Array.Empty<byte>(), store.GetFamily(table), options) != null;
        }

        public byte[] TryGet(byte table, byte[] key)
        {
            return db.Get(key ?? Array.Empty<byte>(), store.GetFamily(table), options);
        }

        public void Dispose()
        {
            snapshot.Dispose();
            batch.Dispose();
        }
    }
}
