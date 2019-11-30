using RocksDbSharp;
using System;
using System.Collections.Generic;

namespace Neo.Storage.RocksDB
{
    public class RocksDBCore : IDisposable
    {
        #region Families

        internal readonly ColumnFamily[] Families = new ColumnFamily[byte.MaxValue + 1];
        internal readonly ColumnFamily DefaultFamily;

        #endregion

        static RocksDBCore()
        {
            Options.WriteDefaultSync.SetSync(true);
        }

        private readonly RocksDb _rocksDb;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="db">Database</param>
        private RocksDBCore(RocksDb db)
        {
            _rocksDb = db ?? throw new NullReferenceException(nameof(db));

            // Get column families

            for (int x = 0; x <= byte.MaxValue; x++)
            {
                Families[x] = new ColumnFamily(db, x.ToString());
            }

            DefaultFamily = new ColumnFamily("", _rocksDb.GetDefaultColumnFamily());
        }

        /// <summary>
        /// Open database
        /// </summary>
        /// <returns>DB</returns>
        public static RocksDBCore Open()
        {
            return Open(Options.Default);
        }

        /// <summary>
        /// Open database
        /// </summary>
        /// <param name="config">Configuration</param>
        /// <returns>DB</returns>
        public static RocksDBCore Open(Options config)
        {
            var families = new ColumnFamilies();

            for (int x = 0; x <= byte.MaxValue; x++)
            {
                families.Add(new ColumnFamilies.Descriptor(x.ToString(), new ColumnFamilyOptions()));
            }

            return new RocksDBCore(RocksDb.Open(config.Build(), config.FilePath, families));
        }

        /// <summary>
        /// Free resources
        /// </summary>
        public void Dispose()
        {
            _rocksDb.Dispose();
        }

        internal IEnumerable<T> Find<T>(ColumnFamily family, ReadOptions options, byte[] prefix, Func<byte[], byte[], T> resultSelector)
        {
            using (var it = _rocksDb.NewIterator(family.Handle, options))
            {
                for (it.Seek(prefix); it.Valid(); it.Next())
                {
                    var key = it.Key();
                    byte[] y = prefix;
                    if (key.Length < y.Length) break;
                    if (!key.AsSpan().StartsWith(y)) break;
                    yield return resultSelector(key, it.Value());
                }
            }
        }

        public void Delete(ColumnFamily family, WriteOptions options, byte[] key)
        {
            _rocksDb.Remove(key, family.Handle, options);
        }

        public byte[] Get(ColumnFamily family, ReadOptions options, byte[] key)
        {
            var value = _rocksDb.Get(key, family.Handle, options);

            if (value == null)
                throw new RocksDbSharpException("not found");

            return value;
        }

        public bool TryGet(ColumnFamily family, ReadOptions options, byte[] key, out byte[] value)
        {
            value = _rocksDb.Get(key, family.Handle, options);
            return value != null;
        }

        public void Put(ColumnFamily family, WriteOptions options, byte[] key, byte[] value)
        {
            _rocksDb.Put(key, value, family.Handle, options);
        }

        public Snapshot GetSnapshot()
        {
            return _rocksDb.CreateSnapshot();
        }

        public Iterator NewIterator(ColumnFamily family, ReadOptions options)
        {
            return _rocksDb.NewIterator(family.Handle, options);
        }

        public void Write(WriteOptions options, WriteBatch batch)
        {
            _rocksDb.Write(batch, options);
        }

        public void Clear(ColumnFamily familiy)
        {
            // Drop the column family
            _rocksDb.DropColumnFamily(familiy.Name);

            // The handle is invalid now, require to obtains a new column family
            familiy.Handle = new ColumnFamily(_rocksDb, familiy.Name).Handle;
        }
    }
}
