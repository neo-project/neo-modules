using Neo.Persistence;
using System;
using System.Collections.Generic;

namespace Neo.Plugins.Storage.Plugins.Storage
{
    internal class Snapshot : ISnapshot
    {
        private readonly Store store;
        private Guid checkpoint;

        public Snapshot(Store store)
        {
            this.store = store;
            store.db.TakeFullCheckpoint(out this.checkpoint);
        }

        public void Commit()
        {
            store.db.CompleteCheckpoint(true);
            checkpoint = Guid.Empty;
        }

        public void Dispose()
        {
            if (checkpoint == Guid.Empty) return;

            store.db.Recover(checkpoint);
            checkpoint = Guid.Empty;
        }

        public void Delete(byte table, byte[] key)
        {
            store.Delete(table, key);
        }

        public IEnumerable<(byte[] Key, byte[] Value)> Find(byte table, byte[] prefix)
        {
            return store.Find(table, prefix);
        }

        public void Put(byte table, byte[] key, byte[] value)
        {
            store.Put(table, key, value);
        }

        public byte[] TryGet(byte table, byte[] key)
        {
            return store.TryGet(table, key);
        }
    }
}
