using Neo.IO.Data.LevelDB;
using Neo.Persistence;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Neo.Plugins.Storage
{
    internal class Store : IStore
    {
        private const byte SYS_Version = 0xf0;
        private readonly DB db;

        public Store(string path)
        {
            this.db = DB.Open(path, new Options { CreateIfMissing = true });
            byte[] value = db.Get(ReadOptions.Default, Helper.CreateKey(SYS_Version));
            if (value != null && Version.TryParse(Encoding.ASCII.GetString(value), out Version version) && version >= Version.Parse("3.0.0"))
                return;

            WriteBatch batch = new WriteBatch();

            if (value != null)
            {
                // Clean all entries only if the version are different

                ReadOptions options = new ReadOptions { FillCache = false };
                using (Iterator it = db.NewIterator(options))
                {
                    for (it.SeekToFirst(); it.Valid(); it.Next())
                    {
                        batch.Delete(it.Key());
                    }
                }
            }

            db.Put(WriteOptions.Default, Helper.CreateKey(SYS_Version), Encoding.ASCII.GetBytes(Assembly.GetExecutingAssembly().GetName().Version.ToString()));
            db.Write(WriteOptions.Default, batch);
        }

        public void Delete(byte table, byte[] key)
        {
            db.Delete(WriteOptions.Default, Helper.CreateKey(table, key));
        }

        public void Dispose()
        {
            db.Dispose();
        }

        public IEnumerable<(byte[], byte[])> Find(byte table, byte[] prefix)
        {
            return db.Find(ReadOptions.Default, Helper.CreateKey(table, prefix), (k, v) => (k[1..], v));
        }

        public ISnapshot GetSnapshot()
        {
            return new Snapshot(db);
        }

        public void Put(byte table, byte[] key, byte[] value)
        {
            db.Put(WriteOptions.Default, Helper.CreateKey(table, key), value);
        }

        public void PutSync(byte table, byte[] key, byte[] value)
        {
            db.Put(WriteOptions.SyncWrite, Helper.CreateKey(table, key), value);
        }

        public byte[] TryGet(byte table, byte[] key)
        {
            return db.Get(ReadOptions.Default, Helper.CreateKey(table, key));
        }
    }
}
