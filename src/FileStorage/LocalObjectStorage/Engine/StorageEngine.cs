using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.LocalObjectStorage.Shards;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.LocalObjectStorage.Engine
{
    public partial class StorageEngine : IDisposable
    {

        private readonly Dictionary<string, Shard> shards = new();
        private readonly ReaderWriterLockSlim mtx = new();

        public void Dispose()
        {
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
                    if (spi is null)
                    {
                        spi = new();
                    }
                    Helper.MergeSplitInfo(e.SplitInfo, spi);
                    if (spi.Link is not null && spi.LastPart is not null)
                        throw new SplitInfoException(spi);
                    continue;
                }
            }
            if (spi is not null) throw new SplitInfoException(spi);
            throw new ObjectNotFoundException();
        }

        public FSObject GetRange(Address address, ulong offset, ulong length)
        {
            SplitInfo spi = null;
            foreach (var shard in SortedShards(address))
            {
                try
                {
                    return shard.GetRange(address, length, offset);
                }
                catch (ObjectNotFoundException)
                {
                    continue;
                }
                catch (SplitInfoException e)
                {
                    if (spi is null)
                    {
                        spi = new();
                    }
                    Helper.MergeSplitInfo(e.SplitInfo, spi);
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
            foreach (var shard in SortedShards(obj.Address))
            {
                var isExist = shard.Exists(obj.Address);
                if (isExist)
                {
                    shard.ToMoveIt(obj.Address);
                    return;
                }

                shard.Put(obj);
                return;
            }
        }

        public void Delete(params Address[] addresses)
        {
            foreach (var address in addresses)
            {
                foreach (var shard in SortedShards(address))
                {
                    if (shard.Exists(address))
                    {
                        shard.Inhume(null, address);
                        return;
                    }
                }
            }
        }

        public List<Address> Select(ContainerID cid, SearchFilters filters)
        {
            var result = new HashSet<Address>();
            foreach (var shard in UnsortedShards())
            {
                var addresses = shard.Select(cid, filters);
                if (addresses?.Any() == true)
                {
                    addresses.ForEach(a => result.Add(a));
                }
            }
            return result.ToList();
        }

        public List<Address> List(ulong limit)
        {
            var result = new HashSet<Address>();
            foreach (var shard in UnsortedShards())
            {
                var addresses = shard.List();
                if (addresses == null) { continue; }
                foreach (var address in addresses)
                {
                    if (!result.Contains(address))
                    {
                        result.Add(address);
                        limit--;
                        if (limit == 0)
                        {
                            return result.ToList();
                        }
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
            var uniqueIDs = new Dictionary<string, ContainerID>();
            foreach (var shard in UnsortedShards())
            {
                var containerIds = shard.ListContainers();
                if (containerIds?.Any() == true)
                {
                    foreach (var containerId in containerIds)
                    {
                        uniqueIDs[containerId.ToString()] = containerId;
                    }
                }
            }

            return uniqueIDs.Values.ToList();
        }




        public bool Exist(Address address)
        {
            foreach (var shard in SortedShards(address))
            {
                var isExist = shard.Exists(address);
                if (isExist)
                {
                    return true;
                }
            }
            return false;
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
                    if (spi is null)
                    {
                        spi = new();
                    }
                    Helper.MergeSplitInfo(e.SplitInfo, spi);
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
                foreach (var shard in SortedShards(address))
                {
                    shard.Inhume(tombstone, address);
                }
            }
        }

        private void Inhume(Address tombstone, Address[] addresses, bool check_exists)
        {
            bool root = false;
            bool RootCheck()
            {
                return check_exists && root;
            }
            foreach (var shard in SortedShards(addresses[0]))
            {
                if (check_exists)
                {
                    try
                    {
                        if (!shard.Exists(addresses[0]) & RootCheck())
                            continue;
                    }
                    catch (ObjectAlreadyRemovedException)
                    {
                        break;
                    }
                    catch (SplitInfoException)
                    {
                        root = true;
                        continue;
                    }
                }
                try
                {
                    shard.Inhume(tombstone, addresses);
                    if (!RootCheck()) break;
                }
                catch
                {
                    continue;
                }
            }
        }
    }
}
