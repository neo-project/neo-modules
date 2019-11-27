using Neo.Persistence;
using RocksDbSharp;
using System.Collections.Generic;

namespace Neo.Storage.RocksDB
{
    internal class RocksDbSnapshot : ISnapshot
    {
        private readonly RocksDBCore db;
        private readonly Snapshot snapshot;
        private readonly WriteBatch batch;
        private readonly ReadOptions options;

        public RocksDbSnapshot(RocksDBCore db)
        {
            this.db = db;
            this.snapshot = db.GetSnapshot();
            this.batch = new WriteBatch();

            options = new ReadOptions();
            options.SetFillCache(false);
            options.SetSnapshot(snapshot);
        }

        public void Commit()
        {
            db.Write(Options.WriteDefault, batch);
        }

        public void Delete(byte table, byte[] key)
        {
            batch.Delete(key, db.Families[table].Handle);
        }

        public void Dispose()
        {
            snapshot.Dispose();
            batch.Dispose();
        }

        public IEnumerable<(byte[] Key, byte[] Value)> Find(byte table, byte[] prefix)
        {
            return db.Find(db.Families[table], options, prefix, (k, v) => (k, v));
        }

        public void Put(byte table, byte[] key, byte[] value)
        {
            batch.Put(key, value, db.Families[table].Handle);
        }

        public byte[] TryGet(byte table, byte[] key)
        {
            if (!db.TryGet(db.Families[table], options, key, out var value))
                return null;
            return value;
        }
    }
}
