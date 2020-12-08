using Neo.IO;
using Neo.IO.Caching;
using Neo.IO.Data.LevelDB;
using System;
using System.Collections.Generic;

namespace Neo.Plugins
{
    public class DbCache<TKey, TValue> : DataCache<TKey, TValue>
        where TKey : IEquatable<TKey>, ISerializable, new()
        where TValue : class, ICloneable<TValue>, ISerializable, new()
    {
        private readonly DB db;
        private readonly ReadOptions options;
        private readonly WriteBatch batch;
        private readonly byte prefix;

        public DbCache(DB db, ReadOptions options, WriteBatch batch, byte prefix)
        {
            this.db = db;
            this.options = options ?? ReadOptions.Default;
            this.batch = batch;
            this.prefix = prefix;
        }

        protected override void AddInternal(TKey key, TValue value)
        {
            batch?.Put(CreateKey(prefix, key), value.ToArray());
        }

        private static byte[] CreateKey(byte prefix, byte[] key = null)
        {
            if (key is null) return new[] { prefix };
            byte[] buffer = new byte[1 + key.Length];
            buffer[0] = prefix;
            Buffer.BlockCopy(key, 0, buffer, 1, key.Length);
            return buffer;
        }

        private static byte[] CreateKey(byte prefix, ISerializable key)
        {
            return CreateKey(prefix, key.ToArray());
        }

        protected override void DeleteInternal(TKey key)
        {
            batch?.Delete(CreateKey(prefix, key));
        }

        protected override IEnumerable<(TKey, TValue)> SeekInternal(byte[] key_prefix, SeekDirection direction)
        {
            return db.Seek(options, prefix, key_prefix, direction, (k, v) => (k.AsSerializable<TKey>(1), v.AsSerializable<TValue>()));
        }

        protected override TValue GetInternal(TKey key)
        {
            return TryGetInternal(key) ?? throw new InvalidOperationException();
        }

        protected override bool ContainsInternal(TKey key)
        {
            return db.Contains(options, CreateKey(prefix, key));
        }

        protected override TValue TryGetInternal(TKey key)
        {
            return db.Get(options, CreateKey(prefix, key))?.AsSerializable<TValue>();
        }

        protected override void UpdateInternal(TKey key, TValue value)
        {
            batch?.Put(CreateKey(prefix, key), value.ToArray());
        }
    }
}
