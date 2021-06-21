using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Google.Protobuf;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.LocalObjectStorage.Blob;
using Neo.FileStorage.LocalObjectStorage.Blobstor;
using Neo.FileStorage.LocalObjectStorage.Metabase;
using Neo.FileStorage.Utils;
using FSAddress = Neo.FileStorage.API.Refs.Address;
using FSObject = Neo.FileStorage.API.Object.Object;


namespace Neo.FileStorage.LocalObjectStorage.Shards
{
    public class Shard : IDisposable
    {
        public ShardID ID { get; init; }
        public BlobStorage BlobStorage { get; init; }
        public MB Metabase { get; init; }
        public IActorRef WorkPool { get; init; }
        public Action<List<FSAddress>, CancellationToken> ExpiredObjectCallback { get; init; }
        private readonly WriteCache writeCache;
        private readonly bool useWriteCache;
        private int mode;
        private CancellationTokenSource source;
        private Task prevGroup;

        public ShardMode Mode
        {
            get => (ShardMode)mode;
            set => Interlocked.Exchange(ref mode, (int)value);
        }

        public Shard(bool useCache)
        {
            useWriteCache = useCache;
            if (useCache)
            {
                writeCache = new WriteCache();
            }
            Mode = ShardMode.Undefined;
        }

        public void Dispose()
        {
            source?.Cancel();
            prevGroup?.Wait();
            BlobStorage?.Dispose();
            Metabase?.Dispose();
        }

        public ulong ContainerSize(ContainerID cid)
        {
            return Metabase.ContainerSize(cid);
        }

        public void Delete(params FSAddress[] addresses)
        {
            var smalls = new Dictionary<FSAddress, BlobovniczaID>();
            foreach (var address in addresses)
            {
                if (useWriteCache)
                {
                    writeCache.Delete(address);
                }
                var blobovniczaID = Metabase.IsSmall(address);
                if (blobovniczaID.IsEmpty)
                {
                    continue;
                }
                smalls[address] = blobovniczaID;
            }

            Metabase.Delete(addresses);
            foreach (var address in addresses)
            {
                if (smalls.ContainsKey(address))
                {
                    BlobStorage.DeleteSmall(address, smalls[address]);
                    continue;
                }
                BlobStorage.DeleteBig(address);
            }
        }


        /// <summary>
        ///  Exists checks if object is presented in shard.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public bool Exists(FSAddress address)
        {
            return Metabase.Exists(address);
        }

        public FSObject Get(FSAddress address)
        {
            if (useWriteCache)
            {
                try
                {
                    var result = writeCache.Get(address);
                    if (result != null)
                    {
                        return result;
                    }
                }
                catch (ObjectNotFoundException) { }
            }
            var isExist = Metabase.Exists(address);
            if (!isExist)
            {
                throw new ObjectNotFoundException();
            }
            var blobovniczaID = Metabase.IsSmall(address);
            if (blobovniczaID is not null)
            {
                return BlobStorage.GetSmall(address, blobovniczaID);
            }
            else
            {
                return BlobStorage.GetBig(address);
            }
        }

        public FSObject Head(FSAddress address, bool raw)
        {
            return Metabase.Get(address, raw)?.CutPayload();
        }


        public void Inhume(FSAddress tombstone, params FSAddress[] target)
        {
            Metabase.Inhume(tombstone, target);
        }

        public List<FSAddress> List()
        {
            var result = new List<FSAddress>();
            var containerIds = Metabase.Containers();
            var filter = new SearchFilters();
            foreach (var containerId in containerIds)
            {
                var addresses = Metabase.Select(containerId, filter);
                if (addresses?.Any() == true)
                {
                    result.AddRange(addresses);
                }
            }
            return result;
        }

        public List<ContainerID> ListContainers()
        {
            return Metabase.Containers();
        }

        public void ToMoveIt(FSAddress address)
        {
            Metabase.MoveIt(address);
        }

        public void Put(FSObject obj)
        {
            if (useWriteCache)
            {
                writeCache.Put(obj);
            }
            else
            {
                var blobovniczaId = BlobStorage.Put(obj);
                if (blobovniczaId != null)
                {
                    Metabase.Put(obj, blobovniczaId);
                }
            }
        }

        public FSObject GetRange(FSAddress address, API.Object.Range range)
        {
            var obj = new FSObject();

            if (useWriteCache)
            {
                var result = writeCache.Get(address);
                if (result != null)
                {
                    obj.Payload = obj.Payload.Range(range.Offset, range.Offset + range.Length);
                    return obj;
                }
            }

            var isExist = Metabase.Exists(address);
            if (!isExist)
            {
                return null;
            }

            var blobovniczaID = Metabase.IsSmall(address);
            if (blobovniczaID != null)
            {
                var small = BlobStorage.GetRangeSmall(address, range, blobovniczaID);
                if (small != null)
                {
                    obj.Payload = ByteString.CopyFrom(small);
                    return obj;
                }
            }
            else
            {
                var big = BlobStorage.GetRangeBig(address, range);
                if (big != null)
                {
                    obj.Payload = ByteString.CopyFrom(big);
                    return obj;
                }
            }
            return null;
        }


        public List<FSAddress> Select(ContainerID cid, SearchFilters filter)
        {
            return Metabase.Select(cid, filter);
        }

        /// <summary>
        ///  WeightValues returns current weight values of the Shard.
        /// </summary>
        /// <returns></returns>
        public ulong WeightValues()
        {
            throw new NotImplementedException();
        }

        public void HandleExpiredTombstones(List<FSAddress> addresses)
        {
            List<FSAddress> inhume = new();
            Metabase.IterateCoveredByTombstones(addresses.ToHashSet(), address =>
            {
                inhume.Add(address);
            });
            if (!inhume.Any()) return;
            Metabase.Inhume(null, inhume.ToArray());
        }

        private void CollectExpiredObjects(ulong epoch, CancellationToken context)
        {
            throw new NotImplementedException();
        }

        private void CollectExpiredTombstones(ulong epoch, CancellationToken context)
        {
            throw new NotImplementedException();
        }

        public void OnNewEpoch(ulong epoch)
        {
            source?.Cancel();
            prevGroup?.Wait();
            source = new();
            Task t1 = new(() =>
            {
                CollectExpiredObjects(epoch, source.Token);
            }, source.Token);
            Task t2 = new(() =>
            {
                CollectExpiredTombstones(epoch, source.Token);
            }, source.Token);
            WorkPool.Tell(new WorkerPool.NewTask
            {
                Process = "Shard.CollectExpiredObjects",
                Task = t1,
            });
            WorkPool.Tell(new WorkerPool.NewTask
            {
                Process = "Shard.CollectExpiredTombstones",
                Task = t2,
            });
            prevGroup = Task.WhenAll(t1, t2);
        }
    }
}
