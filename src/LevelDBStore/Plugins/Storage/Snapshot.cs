using Neo.IO.Caching;
using Neo.IO.Data.LevelDB;
using Neo.Persistence;
using System.Collections.Generic;
using LSnapshot = Neo.IO.Data.LevelDB.Snapshot;

namespace Neo.Plugins.Storage
{
    internal class Snapshot : ISnapshot
    {
        private readonly DB db;
        private readonly LSnapshot snapshot;
        private readonly ReadOptions options;
        private readonly WriteBatch batch;

        public Snapshot(DB db)
        {
            this.db = db;
            this.snapshot = db.GetSnapshot();
            this.options = new ReadOptions { FillCache = false, Snapshot = snapshot };
            this.batch = new WriteBatch();
        }

        public void Commit()
        {
            db.Write(WriteOptions.Default, batch);
        }

        public void Delete(byte table, byte[] key)
        {
            batch.Delete(Helper.CreateKey(table, key));
        }

        public void Dispose()
        {
            snapshot.Dispose();
        }

        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte table, byte[] prefix, SeekDirection direction)
        {
            return db.Seek(options, Helper.CreateKey(table, prefix), direction, (k, v) => (k[1..], v));
        }

        public void Put(byte table, byte[] key, byte[] value)
        {
            batch.Put(Helper.CreateKey(table, key), value);
        }

        public byte[] TryGet(byte table, byte[] key)
        {
            return db.Get(options, Helper.CreateKey(table, key));
        }
    }
}
