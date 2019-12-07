using Neo.Persistence;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Neo.Plugins.Storage
{
    internal class Snapshot : ISnapshot
    {
        private readonly Store store;
        private Guid checkpoint;

        public Snapshot(Store store)
        {
            this.store = store;

            store.db.TakeFullCheckpoint(out checkpoint);
            store.db.CompleteCheckpoint(true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Commit()
        {
            checkpoint = Guid.Empty;
        }

        public void Dispose()
        {
            if (checkpoint == Guid.Empty) return;

            // Recover the checkpoint

            store.db.Recover(checkpoint);
            checkpoint = Guid.Empty;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Delete(byte table, byte[] key)
        {
            store.Delete(table, key);
        }

        public IEnumerable<(byte[] Key, byte[] Value)> Find(byte table, byte[] prefix)
        {
            return store.Find(table, prefix);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Put(byte table, byte[] key, byte[] value)
        {
            store.Put(table, key, value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] TryGet(byte table, byte[] key)
        {
            return store.TryGet(table, key);
        }
    }
}
