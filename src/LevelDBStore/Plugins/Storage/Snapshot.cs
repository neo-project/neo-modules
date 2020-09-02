using Neo.IO.Caching;
using Neo.IO.Data.LevelDB;
using Neo.Persistence;
using System.Collections.Generic;
using LSnapshot = Neo.IO.Data.LevelDB.Snapshot;
using LHelper = Neo.IO.Data.LevelDB.Helper;

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
            batch.Delete(LHelper.CreateKey(table, key));
        }

        public void Dispose()
        {
            snapshot.Dispose();
        }

        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte table, byte[] prefix, SeekDirection direction = SeekDirection.Forward)
        {
            return db.Seek(options, table, prefix, direction, (k, v) => (k[1..], v));
        }

        public void Put(byte table, byte[] key, byte[] value)
        {
            batch.Put(LHelper.CreateKey(table, key), value);
        }

        public bool Contains(byte table, byte[] key)
        {
            return db.Contains(options, LHelper.CreateKey(table, key));
        }

        public byte[] TryGet(byte table, byte[] key)
        {
            return db.Get(options, LHelper.CreateKey(table, key));
        }
    }
}
