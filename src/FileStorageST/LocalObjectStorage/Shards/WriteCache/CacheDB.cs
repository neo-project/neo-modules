using Google.Protobuf;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Database.LevelDB;
using System;
using System.Linq;
using System.Threading;
using static Neo.Helper;

namespace Neo.FileStorage.Storage.LocalObjectStorage.Shards
{
    public class CacheDB : IDisposable
    {
        private static readonly byte[] SequencePrefix = new byte[] { 0x00 };
        private static readonly byte[] ObjectPrefix = new byte[] { 0x01 };
        private static readonly byte[] FlushedKey = new byte[] { 0xFF };

        private readonly DB db;
        private ulong sequence;
        private ulong flushed;

        public CacheDB(string path)
        {
            this.db = new DB(path);
            LoadSequence();
        }

        public void Dispose()
        {
            db?.Dispose();
        }

        private void LoadSequence()
        {
            var bytes = db.Get(FlushedKey);
            if (bytes is null)
            {
                sequence = 0;
                flushed = 0;
                bytes = SequenceToBigEndian(flushed);
                db.Put(FlushedKey, bytes);
            }
            else
            {
                flushed = SequenceFromBigEndian(bytes);
                sequence = flushed;
            }
            db.Iterate(SequencePrefix, bytes, (key, value) =>
            {
                var seq = SequenceFromBigEndian(key[SequencePrefix.Length..]);
                if (seq > sequence) sequence = seq;
                return false;
            });
        }

        private byte[] Objectkey(Address address)
        {
            return Concat(ObjectPrefix, address.ToByteArray());
        }

        private byte[] SequenceToBigEndian(ulong seq)
        {
            var seq_bytes = BitConverter.GetBytes(seq);
            Array.Reverse(seq_bytes);
            return seq_bytes;
        }

        private ulong SequenceFromBigEndian(byte[] seq_bytes)
        {
            Array.Reverse(seq_bytes);
            return BitConverter.ToUInt64(seq_bytes);
        }

        public void Put(ObjectInfo oi)
        {
            var seq = Interlocked.Increment(ref sequence);
            var seq_bytes = SequenceToBigEndian(seq);
            db.Put(Objectkey(oi.Address), Concat(oi.Object.ToByteArray(), seq_bytes));
            db.Put(Concat(SequencePrefix, seq_bytes), oi.Address.ToByteArray());
        }

        public byte[] Get(Address address)
        {
            var data = db.Get(Objectkey(address));
            if (data is not null)
                data = data[..^sizeof(ulong)];
            return data;
        }

        public void Delete(Address address)
        {
            var data = db.Get(Objectkey(address));
            if (data is null) return;
            db.Delete(Objectkey(address));
            db.Delete(Concat(SequencePrefix, data[^sizeof(ulong)..]));
        }

        public void Flushed(Address address)
        {
            var data = db.Get(Objectkey(address));
            if (data is null) return;
            data = data[^sizeof(ulong)..];
            db.Put(FlushedKey, data);
            flushed = SequenceFromBigEndian(data);
        }

        public void Iterate(Func<byte[], bool> handler)
        {
            db.Iterate(ObjectPrefix, (key, value) =>
            {
                return handler(value[..^sizeof(ulong)]);
            });
        }

        public void IterateUnflushed(Func<byte[], bool> handler)
        {
            var exact = Concat(SequencePrefix, SequenceToBigEndian(flushed));
            db.Iterate(SequencePrefix, SequenceToBigEndian(flushed), (key, value) =>
            {
                if (key.SequenceEqual(exact)) return false;
                var raw = db.Get(Concat(ObjectPrefix, value));
                if (raw is null)
                    throw new InvalidOperationException("internal error");
                return handler(raw[..^sizeof(ulong)]);
            });
        }

        public void IterateFormer(Address address, Func<byte[], bool> handler)
        {
            var end = SequenceToBigEndian(sequence);
            var data = db.Get(Objectkey(address));
            if (data is not null) end = data[^sizeof(ulong)..];
            var zero = BitConverter.GetBytes(0ul);
            db.Iterate(SequencePrefix, zero, (key, value) =>
            {
                if (key.AsSpan().SequenceCompareTo(end) > 0) return true;
                var raw = db.Get(Concat(ObjectPrefix, value));
                if (raw is null)
                    throw new InvalidOperationException("internal error");
                return handler(raw[..^sizeof(ulong)]);
            });
        }
    }
}
