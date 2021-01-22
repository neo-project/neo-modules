using Neo.Persistence;
using RocksDbSharp;
using System;
using System.Collections.Generic;
using System.IO;

namespace Neo.Plugins.Storage
{
    internal class Store : IStore
    {
        private readonly RocksDb db;

        public Store(string path)
        {
            db = RocksDb.Open(Options.Default, Path.GetFullPath(path));
        }

        public void Dispose()
        {
            db.Dispose();
        }

        public ISnapshot GetSnapshot()
        {
            return new Snapshot(this, db);
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
            db.Remove(key);
        }

        public void Put(byte[] key, byte[] value)
        {
            db.Put(key, value);
        }

        public void PutSync(byte[] key, byte[] value)
        {
            db.Put(key, value, writeOptions: Options.WriteDefaultSync);
        }
    }
}
