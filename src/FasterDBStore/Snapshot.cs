using FASTER.core;
using Neo.Persistence;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static Neo.Plugins.Storage.Store;

namespace Neo.Plugins.Storage
{
    internal class Snapshot : ISnapshot
    {
        private readonly Store store;
        private Guid checkpoint;
        private long sno = -1;

        public Snapshot(Store store)
        {
            this.store = store;

            store.db.TakeFullCheckpoint(out checkpoint);
            store.db.CompleteCheckpointAsync().GetAwaiter().GetResult();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Commit()
        {
            checkpoint = Guid.Empty;
        }

        public void Dispose()
        {
            if (checkpoint == Guid.Empty) return;
            if (sno == -1) return;
            // Recover the checkpoint

            store.db.Recover(checkpoint);
            checkpoint = Guid.Empty;
            sno = -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Delete(byte table, byte[] key)
        {
            //store.Delete(table, key);

            var k = new BufferKey(table, key);
            store.session.Delete(ref k, Empty.Default, sno++);
            CheckStatus(ref k, sno - 1);
        }

        public IEnumerable<(byte[] Key, byte[] Value)> Find(byte table, byte[] prefix)
        {
            return store.Find(table, prefix);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Put(byte table, byte[] key, byte[] value)
        {
            //store.Put(table, key, value);

            var k = new BufferKey(table, key);
            var v = new BufferValue(value);

            store.session.Upsert(ref k, ref v, Empty.Default, sno++);
            CheckStatus(ref k, sno - 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public byte[] TryGet(byte table, byte[] key)
        {
            return store.TryGet(table, key);
        }

        private void CheckStatus(ref BufferKey key, long serialNo)
        {
            var input = default(Input);
            var output = new Output();

            var status = store.session.Read(ref key, ref input, ref output, Empty.Default, serialNo);

            if (status == Status.PENDING)
            {
                store.session.CompletePending(true);
            }
        }
    }
}
