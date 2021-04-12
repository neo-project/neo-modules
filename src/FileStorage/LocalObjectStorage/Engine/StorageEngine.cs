using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Threading;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.LocalObjectStorage.Engine
{
    public class StorageEngine
    {

        private Dictionary<string, Shard.Shard> shards = new Dictionary<string, Shard.Shard>();
        private ReaderWriterLockSlim mtx = new ReaderWriterLockSlim();


        public FSObject Get(Address address)
        {
            foreach (var shard in SortedShards(address))
            {
                var result = shard.Get(address);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }

        public FSObject GetRange(Address address, ulong offset, ulong length)
        {
            foreach (var shard in SortedShards(address))
            {
                var result = shard.GetRange(address, offset, length);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
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




        public Header Header(Address address, bool raw)
        {
            foreach (var shard in SortedShards(address))
            {
                var result = shard.Head(address, raw);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
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





        private List<Shard.Shard> UnsortedShards()
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


        /// <summary>
        /// todo:实现
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        private List<Shard.Shard> SortedShards(Address address)
        {
            try
            {
                mtx.EnterReadLock();
                //address.GetHashCode()
                return shards.Values.ToList();
            }
            finally
            {
                mtx.ExitReadLock();
            }
        }

        public List<ContainerID> ListContainers()
        {
            throw new NotImplementedException();
        }

        public ulong ContainerSize(ContainerID cid)
        {
            throw new NotImplementedException();
        }
    }
}
