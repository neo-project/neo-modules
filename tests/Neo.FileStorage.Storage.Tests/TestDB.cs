using Neo.FileStorage.Database;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Neo.FileStorage.Storage.Tests
{
    public class TestDB : IDB
    {
        public Dictionary<string, byte[]> Mem = new();

        public byte[] Get(byte[] key)
        {
            if (Mem.TryGetValue(Convert.ToBase64String(key), out var value))
                return value;
            return null;
        }

        public void Put(byte[] key, byte[] value)
        {
            Mem[Convert.ToBase64String(key)] = value;
        }

        public void Delete(byte[] key)
        {
            Mem.Remove(Convert.ToBase64String(key));
        }

        public bool Contains(byte[] key)
        {
            return Mem.ContainsKey(Convert.ToBase64String(key));
        }

        public void Iterate(byte[] prefix, Func<byte[], byte[], bool> handler)
        {
            foreach (var (key, value) in Mem.OrderBy(p => p.Key))
            {
                byte[] k = Convert.FromBase64String(key);
                if (k.AsSpan().StartsWith(prefix))
                {
                    if (handler(k, value)) return;
                }
            }
        }

        public void Iterate(byte[] prefix, byte[] from, Func<byte[], byte[], bool> handler)
        {
            foreach (var (key, value) in Mem.OrderBy(p => p.Key))
            {
                byte[] k = Convert.FromBase64String(key);
                if (k.AsSpan().StartsWith(prefix) && Compare(k[prefix.Length..], from) > 0)
                {
                    if (handler(k, value)) return;
                }
            }
        }

        public void Dispose() { }
        private int Compare(byte[] a, byte[] b)
        {
            for (int i = 0; i < a.Length && i < b.Length; i++)
            {
                if (a[i] > b[i]) return 1;
                else if (b[i] > a[i]) return -1;
            }
            if (a.Length > b.Length) return 1;
            else if (b.Length > a.Length) return -1;
            return 0;
        }
    }
}
