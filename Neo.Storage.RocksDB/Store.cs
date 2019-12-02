using Neo.Persistence;
using RocksDbSharp;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Neo.Plugins.Storage
{
    internal class Store : IStore
    {
        private const byte SYS_Version = 0xf0;
        private readonly RocksDBCore db;

        public Store(string path)
        {
            db = RocksDBCore.Open(new Options { CreateIfMissing = true, FilePath = path });

            if (db.TryGet(db.DefaultFamily, Options.ReadDefault, new byte[] { SYS_Version }, out var value) &&
                Version.TryParse(value.ToString(), out Version version) &&
                version >= Version.Parse("2.9.1"))
                return;

            using (var batch = new WriteBatch())
            {
                var options = new ReadOptions();
                options.SetFillCache(false);

                // Clean all families

                for (int x = 0; x <= byte.MaxValue; x++)
                {
                    db.DropFamily(db.GetFamily((byte)x));
                }

                // Update version

                db.Put(db.DefaultFamily, Options.WriteDefault, new byte[0], Encoding.UTF8.GetBytes(Assembly.GetExecutingAssembly().GetName().Version.ToString()));
                db.Write(Options.WriteDefault, batch);
            }
        }

        public void Dispose()
        {
            db.Dispose();
        }

        public ISnapshot GetSnapshot()
        {
            return new RocksDbSnapshot(db);
        }

        public IEnumerable<(byte[] Key, byte[] Value)> Find(byte table, byte[] prefix)
        {
            return db.Find(db.GetFamily(table), Options.ReadDefault, prefix, (k, v) => (k, v));
        }

        public byte[] TryGet(byte table, byte[] key)
        {
            if (!db.TryGet(db.GetFamily(table), Options.ReadDefault, key, out var value))
                return null;
            return value;
        }

        public void Delete(byte table, byte[] key)
        {
            db.Delete(db.GetFamily(table), Options.WriteDefault, key);
        }

        public void Put(byte table, byte[] key, byte[] value)
        {
            db.Put(db.GetFamily(table), Options.WriteDefault, key, value);
        }

        public void PutSync(byte table, byte[] key, byte[] value)
        {
            db.Put(db.GetFamily(table), Options.WriteDefaultSync, key, value);
        }
    }
}
