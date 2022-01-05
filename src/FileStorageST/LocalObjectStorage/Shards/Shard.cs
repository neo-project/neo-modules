using Akka.Actor;
using Google.Protobuf;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Storage.LocalObjectStorage.Blob;
using Neo.FileStorage.Storage.LocalObjectStorage.Blobstor;
using Neo.FileStorage.Storage.LocalObjectStorage.Metabase;
using System;
using System.Collections.Generic;
using System.Threading;
using FSAddress = Neo.FileStorage.API.Refs.Address;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.LocalObjectStorage.Shards
{
    public partial class Shard : IDisposable
    {

        public const bool DefaultUseWriteCache = true;
        public ShardID ID { get; private set; }
        private readonly bool useWriteCache;
        private readonly BlobStorage blobStorage;
        private readonly MB metabase;
        private readonly IActorRef workPool;
        private readonly WriteCache writeCache;

        public Shard(ShardSettings settings, IActorRef wp, Action<List<FSAddress>, CancellationToken> expiredCallback)
        {
            ID = new();
            useWriteCache = settings.UseWriteCache;
            blobStorage = new(settings.BlobStorageSettings);
            metabase = new(settings.MetabaseSettings.Path);
            workPool = wp;
            if (useWriteCache)
            {
                writeCache = new WriteCache(settings.WriteCacheSettings, blobStorage, metabase);
            }
            expiredTomestonesCallback = expiredCallback;
            removeInteral = settings.RemoverInterval <= 0 ? DefaultRemoveInterval : settings.RemoverInterval;
            removeBatchSize = settings.RemoveBatchSize <= 0 ? DefaultRemoveBatchSize : settings.RemoveBatchSize;
        }

        public void Open()
        {
            if (useWriteCache) writeCache.Open();
            blobStorage.Open();
            metabase.Open();
            timer = new(RemoveGarbage, null, removeInteral, removeInteral);
        }

        public void Dispose()
        {
            timer?.Dispose();
            cancellationSource?.Cancel();
            cancellationSource?.Dispose();
            prevGroup?.Wait();
            blobStorage?.Dispose();
            metabase?.Dispose();
        }

        public ulong ContainerSize(ContainerID cid)
        {
            return metabase.ContainerSize(cid);
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
                if (metabase.IsSmall(address, out var blobovniczaID))
                    smalls[address] = blobovniczaID;
            }
            metabase.Delete(addresses);
            foreach (var address in addresses)
            {
                if (smalls.ContainsKey(address))
                {
                    try
                    {
                        blobStorage.DeleteSmall(address, smalls[address]);
                    }
                    catch (Exception e)
                    {
                        Utility.Log(nameof(Shard), LogLevel.Debug, $"can't remove small object from blobstor, address={address.String()}, error={e.Message}");
                    }
                    continue;
                }
                try
                {
                    blobStorage.DeleteBig(address);
                }
                catch (Exception e)
                {
                    Utility.Log(nameof(Shard), LogLevel.Debug, $"can't remove big object from blobstor, address={address.String()}, error={e.Message}");
                }
            }
        }


        /// <summary>
        ///  Exists checks if object is presented in shard.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public bool Exists(FSAddress address)
        {
            return metabase.Exists(address);
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
            var isExist = metabase.Exists(address);
            if (!isExist)
            {
                throw new ObjectNotFoundException();
            }
            if (metabase.IsSmall(address, out var blobovniczaID))
            {
                return blobStorage.GetSmall(address, blobovniczaID);
            }
            else
            {
                return blobStorage.GetBig(address);
            }
        }

        public FSObject Head(FSAddress address, bool raw)
        {
            if (useWriteCache)
            {
                try
                {
                    return writeCache.Head(address);
                }
                catch (Exception e)
                {
                    if (e is not ObjectNotFoundException) throw;
                }
            }
            return metabase.Get(address, raw)?.CutPayload();
        }


        public void Inhume(FSAddress tombstone, params FSAddress[] target)
        {
            if (useWriteCache)
            {
                foreach (var address in target)
                    writeCache.Delete(address);
            }
            metabase.Inhume(tombstone, target);
        }

        public List<FSAddress> List()
        {
            var result = new List<FSAddress>();
            var containerIds = metabase.Containers();
            var filters = new SearchFilters();
            filters.AddPhyFilter();
            foreach (var containerId in containerIds)
            {
                var addresses = metabase.Select(containerId, filters);
                if (addresses?.Count > 0)
                {
                    result.AddRange(addresses);
                }
            }
            return result;
        }

        public HashSet<ContainerID> ListContainers()
        {
            return metabase.Containers();
        }

        public void ToMoveIt(FSAddress address)
        {
            metabase.MoveIt(address);
        }

        public void Put(FSObject obj)
        {
            if (useWriteCache)
            {
                try
                {
                    writeCache.Put(obj);
                    return;
                }
                catch (Exception e)
                {
                    Utility.Log(nameof(Shard), LogLevel.Warning, $"could not put to write cache, trying to blobStor, error={e.Message}");
                }
            }
            var blobovniczaId = blobStorage.Put(obj);
            metabase.Put(obj, blobovniczaId);
        }

        public FSObject GetRange(FSAddress address, API.Object.Range range)
        {
            var obj = new FSObject();

            if (useWriteCache)
            {
                var result = writeCache.Get(address);
                if (result.PayloadSize < range.Offset + range.Length) throw new RangeOutOfBoundsException();
                obj.Payload = result.Payload.Range(range.Offset, range.Offset + range.Length);
                return obj;
            }

            var isExist = metabase.Exists(address);
            if (!isExist)
                throw new ObjectNotFoundException();

            if (metabase.IsSmall(address, out var blobovniczaID))
            {
                var small = blobStorage.GetRangeSmall(address, range, blobovniczaID);
                obj.Payload = ByteString.CopyFrom(small);
                return obj;
            }
            else
            {
                var big = blobStorage.GetRangeBig(address, range);
                obj.Payload = ByteString.CopyFrom(big);
                return obj;
            }
            throw new ObjectNotFoundException();
        }


        public List<FSAddress> Select(ContainerID cid, SearchFilters filter)
        {
            return metabase.Select(cid, filter);
        }

        public ulong WeightValue()
        {
            return (ulong)new Random().Next();
        }
    }
}
