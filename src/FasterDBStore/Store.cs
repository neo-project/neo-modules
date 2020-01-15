using FASTER.core;
using Neo.Persistence;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace Neo.Plugins.Storage
{
    internal class Store : IStore
    {
        internal class Input
        {
            public byte[] Value = null;
        }

        internal class Output
        {
            public BufferValue Value;
        }

        internal class FasterFunctions : IFunctions<BufferKey, BufferValue, Input, Output, Empty>
        {
            public void InitialUpdater(ref BufferKey key, ref Input input, ref BufferValue value) => value.Value = input.Value;
            public void CopyUpdater(ref BufferKey key, ref Input input, ref BufferValue oldValue, ref BufferValue newValue) => newValue = oldValue;
            public bool InPlaceUpdater(ref BufferKey key, ref Input input, ref BufferValue value)
            {
                value.Value = input.Value;
                return true;
            }

            public void SingleReader(ref BufferKey key, ref Input input, ref BufferValue value, ref Output dst) => dst.Value = value;
            public void SingleWriter(ref BufferKey key, ref BufferValue src, ref BufferValue dst) => dst = src;
            public void ConcurrentReader(ref BufferKey key, ref Input input, ref BufferValue value, ref Output dst) => dst.Value = value;
            public bool ConcurrentWriter(ref BufferKey key, ref BufferValue src, ref BufferValue dst) { dst = src; return true; }

            public void ReadCompletionCallback(ref BufferKey key, ref Input input, ref Output output, Empty ctx, Status status) { }
            public void UpsertCompletionCallback(ref BufferKey key, ref BufferValue value, Empty ctx) { }
            public void RMWCompletionCallback(ref BufferKey key, ref Input input, Empty ctx, Status status) { }
            public void DeleteCompletionCallback(ref BufferKey key, Empty ctx) { }
            public void CheckpointCompletionCallback(string sessionId, CommitPoint commitPoint) { }
        }

        private readonly string StorePath;
        private readonly IDevice log, objlog;
        internal readonly FasterKV<BufferKey, BufferValue, Input, Output, Empty, FasterFunctions> db;
        internal ClientSession<BufferKey, BufferValue, Input, Output, Empty, FasterFunctions> session;

        public Store(string path)
        {
            StorePath = path = Path.GetFullPath(path);

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            log = Devices.CreateLogDevice(Path.Combine(path, "hlog.log"), preallocateFile: false, deleteOnClose: false, recoverDevice: true);
            objlog = Devices.CreateLogDevice(Path.Combine(path, "hlog.obj.log"), preallocateFile: false, deleteOnClose: false, recoverDevice: true);

            db = new FasterKV<BufferKey, BufferValue, Input, Output, Empty, FasterFunctions>
                (1L << 20 /* TODO: I don't know a good value for this */, new FasterFunctions(),
                new LogSettings { LogDevice = log, ObjectLogDevice = objlog },
                new CheckpointSettings
                {
                    CheckpointDir = Path.Combine(path, "Snapshot"),
                    CheckPointType = CheckpointType.Snapshot
                },
                new SerializerSettings<BufferKey, BufferValue>
                {
                    keySerializer = () => new BufferKeySerializer(),
                    valueSerializer = () => new BufferValueSerializer()
                }
                );

            // Each thread calls StartSession to register itself with FASTER

            session = db.NewSession();

            if (File.Exists(Path.Combine(path, "session.id")))
            {
                var data = new byte[16];
                using (var file = File.OpenRead(Path.Combine(path, "session.id")))
                {
                    if (file.Read(data, 0, 16) == 16)
                    {
                        // Recover the last storage state

                        db.Recover(new Guid(data));
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ISnapshot GetSnapshot()
        {
            return new Snapshot(this);
        }

        public void Dispose()
        {
            // Make sure operations are completed
            session.CompletePending(true);

            db.TakeFullCheckpoint(out var uid);
            db.CompleteCheckpointAsync();
            File.WriteAllBytes(Path.Combine(StorePath, "session.id"), uid.ToByteArray());

            // Copy entire log to disk, but retain tail of log in memory
            //db.Log.Flush(true);

            // Move entire log to disk and eliminate data from memory as 
            // well. This will serve workload entirely from disk using read cache if enabled.
            // This will *allow* future updates to the store.
            //db.Log.FlushAndEvict(true);

            // Move entire log to disk and eliminate data from memory as 
            // well. This will serve workload entirely from disk using read cache if enabled.
            // This will *prevent* future updates to the store.
            db.Log.DisposeFromMemory();

            session.Dispose();
            db.Dispose();

            log.Close();
            objlog.Close();
        }

        public IEnumerable<(byte[] Key, byte[] Value)> Find(byte table, byte[] prefix)
        {
            using (var iterator = db.Log.Scan(db.Log.BeginAddress, db.Log.TailAddress))
            {
                while (iterator.GetNext(out var info))
                {
                    var key = iterator.GetKey().Key;
                    if (!key.AsSpan().StartsWith(prefix)) break;

                    yield return (key, iterator.GetValue().Value);
                }
            }
        }

        public void Delete(byte table, byte[] key)
        {
            var input = default(Input);
            var output = new Output();

            var k = new BufferKey(table, key);
            session.Delete(ref k, Empty.Default, 0);
            var status = session.Read(ref k, ref input, ref output, Empty.Default, 0);

            if (status == Status.PENDING)
            {
                session.CompletePending(true);
            }
        }

        public void Put(byte table, byte[] key, byte[] value)
        {
            var input = default(Input);
            var output = new Output();

            var k = new BufferKey(table, key);
            var v = new BufferValue(value);

            session.Upsert(ref k, ref v, Empty.Default, 0);
            var status = session.Read(ref k, ref input, ref output, Empty.Default, 0);

            if (status == Status.PENDING)
            {
                session.CompletePending(true);
            }
        }

        public byte[] TryGet(byte table, byte[] key)
        {
            var k = new BufferKey(table, key);
            var input = default(Input);
            var g1 = new Output();

            var status = session.Read(ref k, ref input, ref g1, Empty.Default, 0);

            if (status == Status.OK)
            {
                return g1.Value.Value;
            }
            else if (status == Status.PENDING)
            {
                session.CompletePending(true);

                // TODO: shall we read it again?
            }

            return null;
        }
    }
}
