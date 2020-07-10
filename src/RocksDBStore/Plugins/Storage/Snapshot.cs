using Neo.IO.Caching;
using Neo.Persistence;
using RocksDbSharp;
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

        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte table, byte[] keyOrPrefix, SeekDirection direction)
        {
            using var it = db.NewIterator(store.GetFamily(table), options);
            for (it.Seek(keyOrPrefix); it.Valid();)
            {
                yield return (it.Key(), it.Value());

                if (direction == SeekDirection.Forward)
                    it.Next();
                else
                    it.Prev();
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
