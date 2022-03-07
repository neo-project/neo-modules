using Google.Protobuf;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Storage.Core.Object;
using Neo.FileStorage.Storage.LocalObjectStorage.Shards;
using Neo.FileStorage.Storage.Services;
using Neo.FileStorage.Storage.Services.Object.Acl.EAcl;
using Neo.FileStorage.Storage.Services.Object.Put;
using Neo.FileStorage.Storage.Services.Object.Search;
using Neo.FileStorage.Storage.Services.Police;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.LocalObjectStorage.Engine
{
    public sealed class StorageEngine : IObjectInhumer, ILocalHeadSource, ILocalSearchSource, ILocalObjectSource, ILocalObjectStore, IObjectListSource, IDisposable
    {
        public const string DefaultPath = "./Data";
        private readonly Dictionary<ShardID, Shard> shards = new();
        private readonly ReaderWriterLockSlim mtx = new();

        public void Open()
        {
            foreach (var shard in shards.Values)
                shard.Open();
        }

        public void Dispose()
        {
            foreach (var shard in shards.Values)
                shard.Dispose();
            shards.Clear();
            mtx.Dispose();
        }

        public FSObject Get(Address address)
        {
            SplitInfo spi = null;
            foreach (var shard in SortedShards(address))
            {
                try
                {
                    return shard.Get(address);
                }
                catch (ObjectNotFoundException)
                {
                    continue;
                }
                catch (SplitInfoException e)
                {
                    if (spi is null) spi = new();
                    spi.MergeSplitInfo(e.SplitInfo);
                    if (spi.Link is not null && spi.LastPart is not null)
                        throw new SplitInfoException(spi);
                    continue;
                }
            }
            if (spi is not null) throw new SplitInfoException(spi);
            throw new ObjectNotFoundException();
        }

        public FSObject GetRange(Address address, API.Object.Range range)
        {
            SplitInfo spi = null;
            foreach (var shard in SortedShards(address))
            {
                try
                {
                    return shard.GetRange(address, range);
                }
                catch (ObjectNotFoundException)
                {
                    continue;
                }
                catch (SplitInfoException e)
                {
                    if (spi is null) spi = new();
                    spi.MergeFrom(e.SplitInfo);
                    if (spi.Link is not null && spi.LastPart is not null)
                        throw new SplitInfoException(spi);
                    continue;
                }
            }
            if (spi is not null) throw new SplitInfoException(spi);
            throw new ObjectNotFoundException();
        }

        public void Put(FSObject obj)
        {
            Utility.Log(nameof(StorageEngine), LogLevel.Debug, $"put object, address={obj.Address.String()}");
            bool put = false;
            bool isExist = Exist(obj.Address);
            string error = "";
            foreach (var shard in SortedShards(obj.Address))
            {
                try
                {
                    if (shard.Exists(obj.Address))
                    {
                        if (!put) return;
                        shard.ToMoveIt(obj.Address);
                        return;
                    };
                    if (put) continue;
                    try
                    {
                        shard.Put(obj);
                        if (!isExist) return;
                        put = true;
                    }
                    catch (Exception e)
                    {
                        error = e.Message;
                        Utility.Log(nameof(StorageEngine), LogLevel.Debug, $"could not put in shard, try another, error={error}");
                    }
                }
                catch { continue; }
            }
            if (!put) throw new InvalidOperationException(error);
        }

        public void Delete(params Address[] addresses)
        {
            foreach (var address in addresses)
            {
                foreach (var shard in SortedShards(address))
                {
                    try
                    {
                        if (shard.Exists(address))
                        {
                            shard.Inhume(null, address);
                            return;
                        }
                    }
                    catch (Exception e)
                    {
                        Utility.Log(nameof(StorageEngine), LogLevel.Debug, $"{nameof(Delete)} could not check object existence, error={e.Message}");
                        continue;
                    }
                }
            }
        }

        public List<Address> Select(ContainerID cid, SearchFilters filters)
        {
            var result = new HashSet<Address>();
            foreach (var shard in UnsortedShards())
                foreach (var address in shard.Select(cid, filters))
                    result.Add(address);
            return result.ToList();
        }

        public List<Address> List(ulong limit)
        {
            var result = new HashSet<Address>();
            var random = new Random();
            foreach (var shard in UnsortedShards().OrderBy(_ => random.Next()))
            {
                foreach (var address in shard.List().OrderBy(_ => random.Next()))
                {
                    if (result.Add(address))
                    {
                        limit--;
                        if (limit == 0) return result.ToList();
                    }
                }
            }
            return result.ToList();
        }

        public ulong ContainerSize(ContainerID id)
        {
            ulong total = 0;
            foreach (var shard in UnsortedShards())
            {
                var size = shard.ContainerSize(id);
                total += size;
            }
            return total;
        }

        public List<ContainerID> ListContainers()
        {
            var ids = new List<ContainerID>();
            foreach (var shard in UnsortedShards())
                ids = ids.Union(shard.ListContainers()).ToList();
            return ids;
        }

        public bool Exist(Address address)
        {
            foreach (var shard in SortedShards(address))
            {
                try
                {
                    if (shard.Exists(address)) return true;
                }
                catch (ObjectAlreadyRemovedException) { throw; }
                catch { continue; }
            }
            return false;
        }

        public FSObject Head(Address address)
        {
            return Head(address, false);
        }

        public FSObject Head(Address address, bool raw)
        {
            SplitInfo spi = null;
            foreach (var shard in SortedShards(address))
            {
                try
                {
                    return shard.Head(address, raw);
                }
                catch (ObjectNotFoundException)
                {
                    continue;
                }
                catch (SplitInfoException e)
                {
                    if (spi is null) spi = new();
                    spi.MergeSplitInfo(e.SplitInfo);
                    if (spi.Link is not null && spi.LastPart is not null)
                        throw new SplitInfoException(spi);
                    continue;
                }
            }
            if (spi is not null) throw new SplitInfoException(spi);
            throw new ObjectNotFoundException();
        }

        public void Inhume(Address tombstone, params Address[] addresses)
        {
            foreach (var address in addresses)
            {
                if (!Inhume(tombstone, address, true))
                    Inhume(tombstone, address, false);
            }
        }

        private bool Inhume(Address tombstone, Address address, bool check_exists)
        {
            bool root = false;
            bool ok = false;
            foreach (var shard in SortedShards(address))
            {
                if (check_exists)
                {
                    try
                    {
                        if (!shard.Exists(address))
                            continue;
                    }
                    catch (ObjectAlreadyRemovedException)
                    {
                        return true;
                    }
                    catch (SplitInfoException)
                    {
                        root = true;
                    }
                    catch (Exception e)
                    {
                        Utility.Log(nameof(StorageEngine), LogLevel.Debug, $"could not check exists when inhume, error={e.Message}");
                        continue;
                    }
                }
                try
                {
                    shard.Inhume(tombstone, address);
                    ok = true;
                    if (!root) return true;
                }
                catch (Exception e)
                {
                    Utility.Log(nameof(StorageEngine), LogLevel.Debug, $"could not inhume address, address={address}, error={e.Message}");
                    continue;
                }
            }
            return ok;
        }

        public void AddShard(Shard shard)
        {
            shards[shard.ID] = shard;
        }

        public void ProcessExpiredTomstones(List<Address> addresses, CancellationToken cancellation)
        {
            var tss = addresses.ToHashSet();
            if (tss.Count == 0) return;
            foreach (var shard in UnsortedShards())
            {
                shard.HandleExpiredTombstones(tss);
                if (cancellation.IsCancellationRequested) return;
            }
        }

        private List<Shard> UnsortedShards()
        {
            try
            {
                mtx.EnterReadLock();
                return shards.Values.ToList();
            }
            finally
            {
                mtx.ExitReadLock();
            }
        }

        private List<Shard> SortedShards(Address address)
        {
            try
            {
                mtx.EnterReadLock();
                var target = address.ToByteArray().Murmur64(0);
                var list = shards.Values.Select(s => new ShardDistance
                {
                    Shard = s,
                    Weight = s.WeightValue(),
                    Distance = Utility.StrictUTF8.GetBytes(s.ID.ToString()).Murmur64(0).Distance(target),
                });
                return list.OrderBy(s => s.Sort).Select(s => s.Shard).ToList();
            }
            finally
            {
                mtx.ExitReadLock();
            }
        }

        private class ShardDistance
        {
            public Shard Shard;
            public ulong Weight;
            public ulong Distance;
            public ulong Sort;

            public ShardDistance SetSort()
            {
                Sort = Weight * Distance;
                return this;
            }
        }
    }
}
