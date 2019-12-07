using FASTER.core;
using Neo.Persistence;
using Neo.Plugins.Storage.Helper;
using System;
using System.Collections.Generic;
using System.IO;

namespace Neo.Plugins.Storage.Plugins.Storage
{
    internal class Store : IStore
    {
        public class Input
        {
            public byte[] Value;
        }

        public class Output
        {
            public BufferValue Value;
        }

        public class FasterFunctions : IFunctions<BufferKey, BufferValue, Input, Output, Empty>
        {
            public void InitialUpdater(ref BufferKey key, ref Input input, ref BufferValue value) => value.Value = input.Value;
            public void CopyUpdater(ref BufferKey key, ref Input input, ref BufferValue oldValue, ref BufferValue newValue) => newValue = oldValue;
            public bool InPlaceUpdater(ref BufferKey key, ref Input input, ref BufferValue value)
            {
                // TODO: What is this
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
            public void CheckpointCompletionCallback(Guid sessionId, long serialNum) { }
        }

        internal readonly FasterKV<BufferKey, BufferValue, Input, Output, Empty, FasterFunctions> db;

        public Store(string path)
        {
            path = Path.GetFullPath(path);

            var log = Devices.CreateLogDevice(Path.Combine(path, "hlog.log"));
            var objlog = Devices.CreateLogDevice(Path.Combine(path, "hlog.obj.log"));

            db = new FasterKV<BufferKey, BufferValue, Input, Output, Empty, FasterFunctions>
                (1L << 20, new FasterFunctions(),
                new LogSettings { LogDevice = log, ObjectLogDevice = objlog, MemorySizeBits = 29 },
                new CheckpointSettings { CheckpointDir = Path.Combine(path, "Snapshot"), CheckPointType = CheckpointType.Snapshot },
                new SerializerSettings<BufferKey, BufferValue>
                {
                    keySerializer = () => new BufferKeySerializer(),
                    valueSerializer = () => new BufferValueSerializer()
                }
                );

            // Each thread calls StartSession to register itself with FASTER
            db.StartSession();
        }

        public ISnapshot GetSnapshot()
        {
            return new Snapshot(this);
        }

        public void Dispose()
        {
            db.StopSession();
            db.Dispose();
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
            var k = new BufferKey(table, key);

            db.Delete(ref k, Empty.Default, 0);
        }

        public void Put(byte table, byte[] key, byte[] value)
        {
            var k = new BufferKey(table, key);
            var v = new BufferValue(value);

            db.Upsert(ref k, ref v, Empty.Default, 0);
        }

        public byte[] TryGet(byte table, byte[] key)
        {
            var k = new BufferKey(table, key);
            var input = default(Input);
            var g1 = new Output();

            if (db.Read(ref k, ref input, ref g1, Empty.Default, 0) == Status.OK)
                return g1.Value.Value;

            return new byte[0];
        }
    }
}
