using Neo.IO.Caching;
using Neo.IO.Data.LevelDB;
using Neo.Persistence;
using System.Collections.Generic;
using LHelper = Neo.IO.Data.LevelDB.Helper;

namespace Neo.Plugins.StateService.StateStorage.LevelDB
{
    public class Store : IStore
    {
        private readonly DB db;

        public Store(string path)
        {
            this.db = DB.Open(path, new Options { CreateIfMissing = true });
        }

        public void Delete(byte table, byte[] key)
        {
            db.Delete(WriteOptions.Default, LHelper.CreateKey(table, key));
        }

        public void Dispose()
        {
            db.Dispose();
        }

        public IEnumerable<(byte[], byte[])> Seek(byte table, byte[] prefix, SeekDirection direction = SeekDirection.Forward)
        {
            return db.Seek(ReadOptions.Default, table, prefix, direction, (k, v) => (k[1..], v));
        }

        public ISnapshot GetSnapshot()
        {
            return new Snapshot(db);
        }

        public void Put(byte table, byte[] key, byte[] value)
        {
            db.Put(WriteOptions.Default, LHelper.CreateKey(table, key), value);
        }

        public void PutSync(byte table, byte[] key, byte[] value)
        {
            db.Put(WriteOptions.SyncWrite, LHelper.CreateKey(table, key), value);
        }

        public bool Contains(byte table, byte[] key)
        {
            return db.Contains(ReadOptions.Default, LHelper.CreateKey(table, key));
        }

        public byte[] TryGet(byte table, byte[] key)
        {
            return db.Get(ReadOptions.Default, LHelper.CreateKey(table, key));
        }
    }
}
