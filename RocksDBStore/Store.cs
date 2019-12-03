using Neo.Persistence;
using RocksDbSharp;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Neo.Plugins.Storage
{
    internal class Store : IStore
    {
        private static readonly byte[] SYS_Version = { 0xf0 };
        private readonly RocksDb db;
        private readonly Dictionary<byte, ColumnFamilyHandle> _families = new Dictionary<byte, ColumnFamilyHandle>();

        public Store(string path)
        {
            var families = new ColumnFamilies();
            for (int x = 0; x <= byte.MaxValue; x++)
                families.Add(new ColumnFamilies.Descriptor(x.ToString(), new ColumnFamilyOptions()));
            db = RocksDb.Open(Options.Default, path, families);

            ColumnFamilyHandle defaultFamily = db.GetDefaultColumnFamily();
            byte[] value = db.Get(SYS_Version, defaultFamily, Options.ReadDefault);
            if (value != null && Version.TryParse(Encoding.ASCII.GetString(value), out Version version) && version >= Version.Parse("3.0.0"))
                return;

            // Clean all families

            for (int x = 0; x <= byte.MaxValue; x++)
            {
                db.DropColumnFamily(x.ToString());
            }
            _families.Clear();

            // Update version

            db.Put(SYS_Version, Encoding.ASCII.GetBytes(Assembly.GetExecutingAssembly().GetName().Version.ToString()), defaultFamily, Options.WriteDefault);
        }

        public void Dispose()
        {
            db.Dispose();
        }

        /// <summary>
        /// Get family
        /// </summary>
        /// <param name="table">Table</param>
        /// <returns>Return column family</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ColumnFamilyHandle GetFamily(byte table)
        {
            if (!_families.TryGetValue(table, out var family))
            {
                family = db.GetColumnFamily(table.ToString());
                _families.Add(table, family);
            }

            return family;
        }

        public ISnapshot GetSnapshot()
        {
            return new Snapshot(this, db);
        }

        public IEnumerable<(byte[] Key, byte[] Value)> Find(byte table, byte[] prefix)
        {
            using var it = db.NewIterator(GetFamily(table), Options.ReadDefault);
            for (it.Seek(prefix); it.Valid(); it.Next())
            {
                var key = it.Key();
                byte[] y = prefix;
                if (key.Length < y.Length) break;
                if (!key.AsSpan().StartsWith(y)) break;
                yield return (key, it.Value());
            }
        }

        public byte[] TryGet(byte table, byte[] key)
        {
            return db.Get(key, GetFamily(table), Options.ReadDefault);
        }

        public void Delete(byte table, byte[] key)
        {
            db.Remove(key, GetFamily(table), Options.WriteDefault);
        }

        public void Put(byte table, byte[] key, byte[] value)
        {
            db.Put(key, value, GetFamily(table), Options.WriteDefault);
        }

        public void PutSync(byte table, byte[] key, byte[] value)
        {
            db.Put(key, value, GetFamily(table), Options.WriteDefaultSync);
        }
    }
}
