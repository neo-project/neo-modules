using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Cache;
using Neo.FileStorage.Storage.LocalObjectStorage.Blob;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static Neo.Utility;

namespace Neo.FileStorage.Storage.LocalObjectStorage.Blobstor
{
    public class BlobovniczaTree : IDisposable
    {
        public const string DefaultDirName = "Blobovniczas";
        public const byte MaxBlzShallowDepth = 4;
        public const byte MaxBlzShallowWidth = 5;
        public const byte DefaultOpenedCacheSize = 8;
        public const byte DefaultBlzShallowDepth = 2;
        public const byte DefaultBlzShallowWidth = 2;

        private readonly byte shallowDepth;
        private readonly byte shallowWidth;
        private readonly ulong smallSizeLimit;
        private readonly ulong fullSizeLimit;
        private readonly string rootPath;
        private readonly byte cacheSize;
        private readonly object openedLock = new();
        private readonly object activeLock = new();
        private readonly LRUCache<string, Blobovnicza> opened;
        private readonly ConcurrentDictionary<string, BlobovniczaWithIndex> active = new();

        public BlobovniczaTree(string path, BlobovniczasSettings settings, ulong smallSize)
        {
            rootPath = path;
            shallowDepth = settings.ShallowDepth <= 0 || settings.ShallowDepth > MaxBlzShallowDepth ? DefaultBlzShallowDepth : settings.ShallowDepth;
            shallowWidth = settings.ShallowWidth <= 0 || settings.ShallowWidth > MaxBlzShallowWidth ? DefaultBlzShallowWidth : settings.ShallowWidth;
            smallSizeLimit = smallSize;
            fullSizeLimit = settings.BlobSize;
            cacheSize = settings.OpenCacheSize == 0 ? DefaultOpenedCacheSize : settings.OpenCacheSize;
            opened = new(cacheSize, OnEvicted);
        }

        private void OnEvicted(string key, Blobovnicza value)
        {
            if (active.ContainsKey(key))
                return;
            value.Dispose();
        }

        public void Open()
        {
            bool DoInit(string path)
            {
                try
                {
                    Blobovnicza b = OpenBlobovnicza(path);
                }
                catch (Exception)
                {
                    Log(nameof(BlobovniczaTree), LogLevel.Debug, $"could not open blobovnicza {path}");
                    throw;
                }
                Log(nameof(BlobovniczaTree), LogLevel.Debug, $"blobovnicza opened {path}");
                return false;
            };
            IterateLeaves(null, DoInit);
        }

        public BlobovniczaID Put(Address address, byte[] data)
        {
            BlobovniczaID id = null;
            bool DoPut(string p)
            {
                BlobovniczaWithIndex bi = GetActived(p);
                try
                {
                    bi.Blobovnicza.Put(address, data);
                }
                catch (BlobFullException)
                {
                    Log(nameof(BlobovniczaTree), LogLevel.Debug, "blobovnicza overflowed, " + Path.Join(p, BitConverter.GetBytes(bi.Index).ToHexString()));
                    UpdateActive(p, bi.Index);
                    return DoPut(p);
                }
                id = bi.Blobovnicza.Path;
                Log(nameof(BlobovniczaTree), LogLevel.Debug, "object successfully saved in active blobovnicza, path:" + bi.Blobovnicza.Path + " addr:" + address.String());
                return true;
            };
            IterateDeepest(address, DoPut);
            if (id is null) throw new InvalidOperationException("could not save the object in any blobovnicza");
            return id;
        }

        public byte[] Get(Address address, BlobovniczaID id = null)
        {
            if (id is not null)
            {
                var blz = OpenBlobovnicza(id);
                return blz.Get(address);
            }
            HashSet<string> cache = new();
            byte[] obj = null;
            bool DoGet(string path)
            {
                var dir = Path.GetDirectoryName(path);
                try
                {
                    cache.Add(dir);
                    return OperateFromLevel(path, !cache.Contains(dir), blz =>
                    {
                        try
                        {
                            obj = blz.Get(address);
                        }
                        catch (Exception e) when (e is not ObjectNotFoundException)
                        {
                            Log(nameof(BlobovniczaTree), LogLevel.Debug, $"could not read object from active blobovnicza, error={e.Message}");
                            return false;
                        }
                        return true;
                    });
                }
                catch (ObjectNotFoundException)
                {
                    Log(nameof(BlobovniczaTree), LogLevel.Debug, $"could not get object from level, level={path}");
                    return false;
                }
                catch (Exception e)
                {
                    Log(nameof(BlobovniczaTree), LogLevel.Debug, $"could not get object from level, level={path}, error={e.Message}");
                    throw;
                }
            };
            IterateLeaves(address, DoGet);
            if (obj is null) throw new ObjectNotFoundException();
            return obj;
        }

        public void Delete(Address address, BlobovniczaID id = null)
        {
            if (id is not null)
            {
                Blobovnicza b = OpenBlobovnicza(id);
                b.Delete(address);
                return;
            }
            HashSet<string> cache = new();
            bool DoDelete(string path)
            {
                var dir = Path.GetDirectoryName(path);
                try
                {
                    cache.Add(dir);
                    return OperateFromLevel(path, !cache.Contains(dir), blz =>
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
                }
                catch (ObjectNotFoundException)
                {
                    Log(nameof(BlobovniczaTree), LogLevel.Debug, $"could not get object from level, level={path}");
                    return false;
                }
                catch (Exception e)
                {
                    Log(nameof(BlobovniczaTree), LogLevel.Debug, $"could not get object from level, level={path}, error={e.Message}");
                    throw;
                }
            };
            if (!IterateLeaves(address, DoDelete))
                throw new ObjectNotFoundException();
        }

        private bool OperateFromLevel(string path, bool try_active, Func<Blobovnicza, bool> func)
        {
            var level_path = Path.GetDirectoryName(path);
            if (opened.TryGet(path, out Blobovnicza blz))
                if (func(blz))
                    return true;
            if (active.TryGetValue(level_path, out BlobovniczaWithIndex bi) && try_active)
                if (func(bi.Blobovnicza))
                    return true;
            int index = byte.Parse(Path.GetFileName(path), System.Globalization.NumberStyles.HexNumber);
            if (bi.Index < index) throw new ObjectNotFoundException();
            Blobovnicza b = OpenBlobovnicza(path);
            if (func(b)) return true;
            return false;
        }

        private BlobovniczaWithIndex GetActived(string path)
        {
            return UpdateAndGet(path, null);
        }

        private void UpdateActive(string path, byte old)
        {
            Log(nameof(BlobovniczaTree), LogLevel.Debug, "updating active blobovnicza...");
            UpdateAndGet(path, old);
            Log(nameof(BlobovniczaTree), LogLevel.Debug, "active blobovnicza successfully updated");
        }

        private BlobovniczaWithIndex UpdateAndGet(string path, byte? old)
        {
            lock (activeLock)
            {
                bool exist = active.TryGetValue(path, out BlobovniczaWithIndex bi);
                if (exist)
                {
                    if (old is null)
                        return bi;
                    if (bi.Index == shallowWidth - 1)
                        throw new InvalidOperationException("no more blobovniczas");
                    if (bi.Index != old)
                        return bi;
                    bi.Index++;
                }
                else
                    bi = new() { Index = 0 };
                bi.Blobovnicza = OpenBlobovnicza(Path.Join(path, bi.Index.ToString("x2")));
                if (active.TryGetValue(path, out BlobovniczaWithIndex b) && bi.Blobovnicza.Equals(b.Blobovnicza)) return b;
                active[path] = bi;
                opened.Remove(path);
                return bi;
            }
        }

        private bool IterateLeaves(Address address, Func<string, bool> func)
        {
            return IterateSorted(address, new List<string>() { rootPath }, shallowDepth, paths => func(Path.Join(paths.ToArray())));
        }

        private void IterateDeepest(Address address, Func<string, bool> func)
        {
            var depth = shallowDepth - 1;
            if (depth == 0)
                func(rootPath);
            else
                IterateSorted(address, new List<string>() { rootPath }, depth, paths => func(Path.Join(paths.ToArray())));
        }

        public bool IterateSorted(Address address, List<string> current_path, int depth, Func<List<string>, bool> func)
        {
            byte[] indices = IndexSlice(shallowWidth);
            ulong hash = AddressHash(address, Path.Join(current_path.ToArray()));
            var sorted = indices.OrderBy(p => ((ulong)p).Distance(hash)).ToArray();
            bool exec = current_path.Count == depth;
            for (int i = 0; i < sorted.Length; i++)
            {
                if (i == 0)
                    current_path.Add(sorted[i].ToString("x2"));
                else
                    current_path[^1] = sorted[i].ToString("x2");
                if (exec)
                {
                    if (func(current_path)) return true;
                }
                else
                {
                    if (IterateSorted(address, current_path.ToList(), depth, func)) return true;
                }
            }
            return false;
        }

        private byte[] IndexSlice(byte number)
        {
            byte[] s = new byte[number];
            for (byte i = 0; i < number; i++)
                s[i] = i;
            return s;
        }

        private ulong AddressHash(Address address, string path)
        {
            return StrictUTF8.GetBytes(address?.String() + path).Murmur64(0);
        }


        private Blobovnicza OpenBlobovnicza(string path)
        {
            lock (openedLock)
            {
                if (opened.TryGet(path, out Blobovnicza b))
                    return b;
                b = new Blobovnicza(path)
                {
                    FullSizeLimit = fullSizeLimit,
                    ObjSizeLimit = smallSizeLimit,
                };
                b.Open();
                opened.Add(path, b);
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
