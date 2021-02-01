using Neo.Persistence;
using RocksDbSharp;
using System;
using System.Collections.Generic;
using System.IO;

namespace Neo.Plugins.Storage
{
    public class RocksDbStore : IStore
    {
        private readonly RocksDb db;
        private readonly bool readOnly;

        private RocksDbStore(RocksDb db, bool readOnly = false)
        {
            this.db = db;
            this.readOnly = readOnly;
        }

        public static RocksDbStore Open(string path)
        {
            var db = RocksDb.Open(Options.Default, Path.GetFullPath(path));
            return new RocksDbStore(db);
        }

        public static RocksDbStore OpenReadOnly(string path)
        {
            var db = RocksDb.OpenReadOnly(Options.Default, Path.GetFullPath(path), false);
            return new RocksDbStore(db, true);
        }

        public void Dispose()
        {
            db.Dispose();
        }

        public Checkpoint Checkpoint() => db.Checkpoint();

        public ISnapshot GetSnapshot()
        {
            return new RocksDbSnapshot(this, db);
        }

        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte[] keyOrPrefix, SeekDirection direction = SeekDirection.Forward)
        {
            if (keyOrPrefix == null) keyOrPrefix = Array.Empty<byte>();

            using var it = db.NewIterator();
            if (direction == SeekDirection.Forward)
                for (it.Seek(keyOrPrefix); it.Valid(); it.Next())
                    yield return (it.Key(), it.Value());
            else
                for (it.SeekForPrev(keyOrPrefix); it.Valid(); it.Prev())
                    yield return (it.Key(), it.Value());
        }

        public bool Contains(byte[] key)
        {
            return db.Get(key) != null;
        }

        public byte[] TryGet(byte[] key)
        {
            return db.Get(key);
        }

        public void Delete(byte[] key)
        {
            if (readOnly) throw new InvalidOperationException();
            db.Remove(key);
        }

        public void Put(byte[] key, byte[] value)
        {
            if (readOnly) throw new InvalidOperationException();
            db.Put(key, value);
        }

        public void PutSync(byte[] key, byte[] value)
        {
            if (readOnly) throw new InvalidOperationException();
            db.Put(key, value, writeOptions: Options.WriteDefaultSync);
        }
    }
}
