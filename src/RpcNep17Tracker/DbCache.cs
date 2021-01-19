using Neo.IO;
using Neo.IO.Caching;
using Neo.IO.Data.LevelDB;
using Neo.Persistence;
using Neo.SmartContract;
using System;
using System.Collections.Generic;

namespace Neo.Plugins
{
    public class DbCache : DataCache
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

        protected override void AddInternal(StorageKey key, StorageItem value)
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

        protected override void DeleteInternal(StorageKey key)
        {
            batch?.Delete(CreateKey(prefix, key));
        }

        protected override IEnumerable<(StorageKey, StorageItem)> SeekInternal(byte[] key_prefix, SeekDirection direction)
        {
            return db.Seek(options, key_prefix, direction, (k, v) => (k.AsSerializable<StorageKey>(1), v.AsSerializable<StorageItem>()));
        }

        protected override StorageItem GetInternal(StorageKey key)
        {
            return TryGetInternal(key) ?? throw new InvalidOperationException();
        }

        protected override bool ContainsInternal(StorageKey key)
        {
            return db.Contains(options, CreateKey(prefix, key));
        }

        protected override StorageItem TryGetInternal(StorageKey key)
        {
            return db.Get(options, CreateKey(prefix, key))?.AsSerializable<StorageItem>();
        }

        protected override void UpdateInternal(StorageKey key, StorageItem value)
        {
            batch?.Put(CreateKey(prefix, key), value.ToArray());
        }
    }
}
