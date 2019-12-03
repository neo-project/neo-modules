using RocksDbSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Neo.Plugins.Storage
{
    public class RocksDBCore : IDisposable
    {
        #region Families

        private readonly Dictionary<byte, ColumnFamily> _families = new Dictionary<byte, ColumnFamily>();
        public readonly ColumnFamily DefaultFamily;

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

            // Get default column families

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

        #region Families

        /// <summary>
        /// Get family
        /// </summary>
        /// <param name="table">Table</param>
        /// <returns>Return column family</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ColumnFamily GetFamily(byte table)
        {
            if (!_families.TryGetValue(table, out var family))
            {
                family = new ColumnFamily(_rocksDb, table.ToString());
                _families.Add(table, family);
            }

            return family;
        }

        /// <summary>
        /// Drop column family
        /// </summary>
        /// <param name="family">Family</param>
        public void DropFamily(ColumnFamily family)
        {
            // Drop the column family

            _rocksDb.DropColumnFamily(family.Name);

            // The handle is invalid now, require to obtains a new column family

            _families.Where(u => u.Value.Name == family.Name).All(u => _families.Remove(u.Key));
        }

        #endregion
    }
}
