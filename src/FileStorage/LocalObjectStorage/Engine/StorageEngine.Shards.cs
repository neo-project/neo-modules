using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Google.Protobuf;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.LocalObjectStorage.Shards;

namespace Neo.FileStorage.LocalObjectStorage.Engine
{
    public partial class StorageEngine : IDisposable
    {
        public ShardID AddShard(string path, bool use_cache)
        {
            ShardID id = ShardID.Generate();
            shards[id.ToString()] = new Shard(use_cache, path)
            {
                ID = id
            };
            return id;
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

        /// <summary>
        /// sort by value-distance * weights
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        private List<Shard> SortedShards(Address address)
        {
            try
            {
                mtx.EnterReadLock();
                var target = address.ToByteArray().Murmur64(0);
                var list = shards.Values.Select(s => new ShardDistance
                {
                    Shard = s,
                    Weight = s.WeightValues(),
                    Distance = Utility.StrictUTF8.GetBytes(s.ID.ToString()).Murmur64(0).Distance(target),
                });
                return list.OrderBy(s => s.Sort).Select(s => s.Shard).ToList();
            }
            finally
            {
                mtx.ExitReadLock();
            }
        }

        class ShardDistance
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
