using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Cache;
using Neo.FileStorage.LocalObjectStorage.Blob;
using Neo.IO.Data.LevelDB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FSObject = Neo.FileStorage.API.Object.Object;
using FSRange = Neo.FileStorage.API.Object.Range;
using static Neo.Utility;

namespace Neo.FileStorage.LocalObjectStorage.Blobstor
{
    public class BlobovniczaTree : IDisposable
    {
        public const int DefaultOpenedCacheSize = 50;
        public const int DefaultBlzShallowDepth = 2;
        public const int DefaultBlzShallowWidth = 16;

        public int OpenedCacheSize { get; init; }
        public ulong BlzShallowDepth { get; init; }
        public ulong BlzShallowWidth { get; init; }
        private readonly ICompressor compressor;
        public string BlzRootPath { get; private set; }
        private readonly LRUCache<string, Blobovnicza> opened;
        private readonly Dictionary<string, BlobovniczaWithIndex> active;

        public BlobovniczaTree(string path, ICompressor compressor = null, int cap = 0)
        {
            BlzShallowDepth = DefaultBlzShallowDepth;
            BlzShallowWidth = DefaultBlzShallowWidth;
            this.compressor = compressor;
            OpenedCacheSize = cap == 0 ? DefaultOpenedCacheSize : cap;
            BlzRootPath = path;
            opened = new(cap, OnEvicted);
            ulong cp = 1;
            for (ulong i = 0; i < BlzShallowDepth; i++)
                cp *= BlzShallowWidth;
            active = new((int)cp);
        }

        private void OnEvicted(string key, Blobovnicza value)
        {
            lock (active)
            {
                if (active.ContainsKey(Path.GetFullPath(key)))
                    return;
            }
            value.Dispose();
        }

        public void Initialize()
        {
            bool DoInit(string path)
            {
                try
                {
                    Blobovnicza b = OpenBlobovnicza(path);
                }
                catch (LevelDBException)
                {
                    Log(nameof(BlobovniczaTree), LogLevel.Debug, $"could not open blobovnicza {path}");
                    return false;
                }
                Log(nameof(BlobovniczaTree), LogLevel.Debug, $"blobovnicza opened {path}");
                return true;
            };
            IterateLeaves(null, DoInit);
        }

        public BlobovniczaID Put(FSObject obj)
        {
            BlobovniczaID id = null;
            bool DoPut(string p)
            {
                BlobovniczaWithIndex bi;
                try
                {
                    bi = GetActived(p);
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is LevelDBException)
                {
                    Log(nameof(BlobovniczaTree), LogLevel.Debug, "could not get active blobovnicza");
                    return false;
                }
                try
                {
                    bi.Blobovnicza.Put(obj);
                }
                catch (BlobFullException)
                {
                    Log(nameof(BlobovniczaTree), LogLevel.Debug, "blobovnicza overflowed, " + Path.Join(p, bi.Index.ToString()));
                    try
                    {
                        UpdateActive(p, bi.Index);
                    }
                    catch (Exception ex) when (ex is InvalidOperationException || ex is LevelDBException)
                    {
                        Log(nameof(BlobovniczaTree), LogLevel.Debug, "could not update active blobovnicza");
                        return false;
                    }
                    return DoPut(p);
                }
                catch (Exception e)
                {
                    Log(nameof(BlobovniczaTree), LogLevel.Debug, "could not put object to active blobovnicza, path:" + Path.Join(p, bi.Index.ToString()) + " error:" + e.Message);
                    return false;
                }
                p = Path.Join(p, bi.Index.ToString());
                id = StrictUTF8.GetBytes(p);
                Log(nameof(BlobovniczaTree), LogLevel.Debug, "object successfully saved in active blobovnicza, path:" + p + " addr:" + obj.Address.String());
                return true;
            };
            IterateDeepest(obj.Address, DoPut);
            if (id is null) throw new InvalidOperationException("could not save the object in any blobovnicza");
            return id;
        }

        public FSObject Get(Address address, BlobovniczaID id = null)
        {
            if (id is not null)
            {
                using var blz = OpenBlobovnicza(id.ToString());
                return blz.Get(address);
            }
            HashSet<string> cache = new();
            FSObject obj = null;
            bool DoGet(string path)
            {
                var dir = Path.GetDirectoryName(path);
                try
                {
                    OperateFromLevel(path, cache.Contains(dir), blz =>
                    {
                        try
                        {
                            obj = blz.Get(address);
                        }
                        catch (Exception e) when (e is not ObjectNotFoundException)
                        {
                            Log(nameof(BlobovniczaTree), LogLevel.Debug, "could not read object from active blobovnicza");
                            return false;
                        }
                        return true;
                    });
                    return true;
                }
                catch (ObjectNotFoundException)
                {
                    Log(nameof(BlobovniczaTree), LogLevel.Debug, $"could not get object from level, level={path}");
                }
                catch (Exception e)
                {
                    Log(nameof(BlobovniczaTree), LogLevel.Debug, $"could not get object from level, level={path}, error={e.Message}");
                }
                cache.Add(dir);
                return false;
            };
            IterateLeaves(address, DoGet);
            if (obj is null) throw new ObjectNotFoundException();
            return obj;
        }

        public void Delete(Address address, BlobovniczaID id = null)
        {
            if (id is not null)
            {
                using Blobovnicza b = OpenBlobovnicza(id.ToString());
                b.Delete(address);
            }
            HashSet<string> cache = new();
            bool DoDelete(string path)
            {
                var dir = Path.GetDirectoryName(path);
                try
                {
                    OperateFromLevel(path, cache.Contains(dir), blz =>
                    {
                        try
                        {
                            blz.Delete(address);
                        }
                        catch (Exception e) when (e is not ObjectNotFoundException)
                        {
                            Log(nameof(BlobovniczaTree), LogLevel.Debug, "could not read object from active blobovnicza");
                            return false;
                        }
                        return true;
                    });
                    return true;
                }
                catch (ObjectNotFoundException)
                {
                    Log(nameof(BlobovniczaTree), LogLevel.Debug, $"could not get object from level, level={path}");
                }
                catch (Exception e)
                {
                    Log(nameof(BlobovniczaTree), LogLevel.Debug, $"could not get object from level, level={path}, error={e.Message}");
                }
                cache.Add(dir);
                return false;
            };
            IterateLeaves(address, DoDelete);
        }

        public byte[] GetRange(Address address, FSRange range, BlobovniczaID id = null)
        {
            if (id is not null)
            {
                using var blz = OpenBlobovnicza(id.ToString());
                return blz.GetRange(address, range);
            }
            HashSet<string> cache = new();
            byte[] data = null;
            bool DoGetRange(string path)
            {
                var dir = Path.GetDirectoryName(path);
                try
                {
                    OperateFromLevel(path, cache.Contains(dir), blz =>
                    {
                        try
                        {
                            data = blz.GetRange(address, range);
                        }
                        catch (Exception e) when (e is not ObjectNotFoundException)
                        {
                            Log(nameof(BlobovniczaTree), LogLevel.Debug, "could not read object from active blobovnicza");
                            return false;
                        }
                        return true;
                    });
                    return true;
                }
                catch (ObjectNotFoundException)
                {
                    Log(nameof(BlobovniczaTree), LogLevel.Debug, $"could not get object from level, level={path}");
                }
                catch (Exception e)
                {
                    Log(nameof(BlobovniczaTree), LogLevel.Debug, $"could not get object from level, level={path}, error={e.Message}");
                }
                cache.Add(dir);
                return false;
            };
            IterateLeaves(address, DoGetRange);
            if (data is null) throw new ObjectNotFoundException();
            return data;
        }

        private void OperateFromLevel(string path, bool try_active, Func<Blobovnicza, bool> func)
        {
            var level_path = Path.GetDirectoryName(path);
            if (opened.TryGet(path, out Blobovnicza blz))
            {
                if (func(blz)) return;
            }
            BlobovniczaWithIndex bi;
            bool exist = false;
            lock (active)
            {
                exist = active.TryGetValue(level_path, out bi);
            }
            if (exist && try_active)
            {
                if (func(bi.Blobovnicza)) return;
            }
            ulong index = BitConverter.ToUInt64(Path.GetFileName(path).HexToBytes());
            if (bi.Index < index) throw new ObjectNotFoundException();
            using Blobovnicza b = OpenBlobovnicza(path);
            func(b);
        }

        private BlobovniczaWithIndex GetActived(string path)
        {
            return UpdateAndGet(path, null);
        }

        private void UpdateActive(string path, ulong old)
        {
            Log(nameof(BlobovniczaTree), LogLevel.Debug, "updating active blobovnicza...");
            UpdateAndGet(path, old);
            Log(nameof(BlobovniczaTree), LogLevel.Debug, "active blobovnicza successfully updated");
        }

        private BlobovniczaWithIndex UpdateAndGet(string path, ulong? old)
        {
            lock (active)
            {
                bool exist = active.TryGetValue(path, out BlobovniczaWithIndex bi);
                if (exist)
                {
                    if (old is null) return bi;
                    if (bi.Index == BlzShallowWidth - 1)
                        throw new InvalidOperationException("no more blobovniczas");
                    if (bi.Index != old)
                        return bi;
                    bi.Index++;
                }
                bi.Blobovnicza = OpenBlobovnicza(Path.Join(path, BitConverter.GetBytes(bi.Index).ToHexString()));
                if (active.TryGetValue(path, out BlobovniczaWithIndex tbi) && tbi.Blobovnicza == bi.Blobovnicza)
                    return tbi;
                opened.Remove(path);
                active[path] = bi;
                return bi;
            }
        }

        private void IterateLeaves(Address address, Func<string, bool> func)
        {
            IterateSorted(address, new List<string>(), BlzShallowDepth, paths => func(Path.Join(paths.ToArray())));
        }

        private void IterateDeepest(Address address, Func<string, bool> func)
        {
            ulong depth = BlzShallowDepth;
            if (0 < depth) depth--;
            IterateSorted(address, new List<string>(), depth, paths => func(Path.Join(paths.ToArray())));
        }

        private bool IterateSorted(Address address, List<string> current_path, ulong depth, Func<List<string>, bool> func)
        {
            List<ulong> indices = IndexSlice(BlzShallowWidth);
            ulong hash = AddressHash(address, Path.Join(current_path.ToArray()));
            List<ulong> sorted = indices.OrderBy(p => p.Distance(hash)).ToList();
            bool exec = (ulong)current_path.Count == depth;
            for (int i = 0; i < sorted.Count; i++)
            {
                if (i == 0)
                    current_path.Add(BitConverter.GetBytes(sorted[i]).ToHexString());
                else
                    current_path[^1] = BitConverter.GetBytes(sorted[i]).ToHexString();
                if (exec)
                {
                    if (func(current_path)) return true;
                }
                else
                {
                    if (IterateSorted(address, current_path, depth, func)) return true;
                }
            }
            return false;
        }

        private List<ulong> IndexSlice(ulong number)
        {
            List<ulong> s = new();
            for (ulong i = 0; i < number; i++)
                s.Add(i);
            return s;
        }

        private ulong AddressHash(Address address, string path)
        {
            return StrictUTF8.GetBytes(address.String() + path).Murmur64(0);
        }


        private Blobovnicza OpenBlobovnicza(string path)
        {
            lock (opened)
            {
                if (opened.TryGet(path, out Blobovnicza b))
                    return b;
                b = new Blobovnicza(Path.Join(BlzRootPath, path))
                {
                    Compressor = compressor,
                };
                b.Open();
                opened.TryAdd(path, b);
                return b;
            }
        }

        public void Dispose()
        {
            lock (active)
            {
                foreach (var b in active.Values)
                    b.Blobovnicza.Dispose();
                active.Clear();
            }
            lock (opened)
            {
                opened.Purge();
            }
        }
    }
}
