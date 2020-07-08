using Neo.IO.Caching;
using Neo.Persistence;
using RocksDbSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

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
            db = RocksDb.Open(Options.Default, Path.GetFullPath(path), families);

            ColumnFamilyHandle defaultFamily = db.GetDefaultColumnFamily();
            byte[] value = db.Get(SYS_Version, defaultFamily, Options.ReadDefault);
            if (value != null && Version.TryParse(Encoding.ASCII.GetString(value), out Version version) && version >= Version.Parse("3.0.0"))
                return;

            if (value != null)
            {
                // Clean all families only if the version are different

                Parallel.For(0, byte.MaxValue + 1, (x) => db.DropColumnFamily(x.ToString()));
                _families.Clear();
            }

            // Update version

            db.Put(SYS_Version, Encoding.ASCII.GetBytes(Assembly.GetExecutingAssembly().GetName().Version.ToString()), defaultFamily, Options.WriteDefault);
        }

        public void Dispose()
        {
            db.Dispose();
            _families.Clear();
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
                try
                {
                    // Try to find the family

                    family = db.GetColumnFamily(table.ToString());
                    _families.Add(table, family);
                }
                catch (KeyNotFoundException)
                {
                    // Try to create the family

                    family = db.CreateColumnFamily(new ColumnFamilyOptions(), table.ToString());
                    _families.Add(table, family);
                }
            }

            return family;
        }

        public ISnapshot GetSnapshot()
        {
            return new Snapshot(this, db);
        }

        public IEnumerable<(byte[] Key, byte[] Value)> Seek(byte table, byte[] prefix, SeekDirection direction = SeekDirection.Forward)
        {
            using var it = db.NewIterator(GetFamily(table), Options.ReadDefault);
            for (it.Seek(prefix); it.Valid();)
            {
                yield return (it.Key(), it.Value());

                if (direction == SeekDirection.Forward)
                    it.Next();
                else
                    it.Prev();
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
