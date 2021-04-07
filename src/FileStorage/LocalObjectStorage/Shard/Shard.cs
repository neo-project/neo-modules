using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using Google.Protobuf;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.LocalObjectStorage.Blob;
using Neo.FileStorage.LocalObjectStorage.Blobstor;
using Neo.FileStorage.LocalObjectStorage.MetaBase;
using FSObject = Neo.FileStorage.API.Object.Object;


namespace Neo.FileStorage.LocalObjectStorage.Shard
{
    public class Shard
    {
        private readonly Blobstorage writeCache;
        private readonly Blobstorage blobStor;
        private readonly MB metaBase;
        private readonly int rmBatchSize;
        private readonly bool useWriteCache;


        public ShardID ID { get; set; }


        private int mode;
        public ShardMode Mode
        {
            get => (ShardMode)mode;
            set => Interlocked.Exchange(ref mode, (int)value);
        }


        public Shard(bool useCache, string path)
        {
            rmBatchSize = 100;
            useWriteCache = useCache;
            if (useCache)
            {
                writeCache = new Blobstorage();
            }
            blobStor = new Blobstorage();
            metaBase = new MB(path);
            Mode = ShardMode.Undefined;
        }

        public ulong ContainerSize(ContainerID cid)
        {
            return metaBase.ContainerSize(cid);
        }

        public void Delete(params Address[] addresses)
        {
            var smalls = new Dictionary<Address, BlobovniczaID>();
            foreach (var address in addresses)
            {
                if (useWriteCache)
                {
                    writeCache.DeleteSmall(address);
                    writeCache.DeleteBig(address);
                }
                var blobovniczaID = metaBase.IsSmall(address);
                if (blobovniczaID.IsEmpty)
                {
                    continue;
                }
                smalls[address] = blobovniczaID;
            }

            metaBase.Delete(addresses);
            foreach (var address in addresses)
            {
                if (smalls.ContainsKey(address))
                {
                    blobStor.DeleteSmall(address, smalls[address]);
                    continue;
                }
                blobStor.DeleteBig(address);
            }
        }


        /// <summary>
        ///  Exists checks if object is presented in shard.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public bool Exists(Address address)
        {
            return metaBase.Exists(address);
        }

        public FSObject Get(Address address)
        {
            if (useWriteCache)
            {
                var smallResult = writeCache.GetSmall(address);
                if (smallResult != null)
                {
                    return smallResult;
                }

                var bigResult = writeCache.GetBig(address);
                if (bigResult != null)
                {
                    return bigResult;
                }
            }
            var isExist = metaBase.Exists(address);
            if (!isExist)
            {
                return null;
            }

            var blobovniczaID = metaBase.IsSmall(address);
            if (blobovniczaID != null)
            {
                return blobStor.GetSmall(address, blobovniczaID);
            }
            else
            {
                return blobStor.GetBig(address);
            }
        }



        public Header Head(Address address, bool raw)
        {
            return metaBase.Get(address, raw)?.Header;
        }


        public void Inhume(Address tombstone, List<Address> target)
        {
            metaBase.Inhume(tombstone, target);
        }

        public List<Address> List()
        {
            var result = new List<Address>();
            var containerIds = metaBase.Containers();
            var filter = new SearchFilters();
            foreach (var containerId in containerIds)
            {
                var addresses = metaBase.Select(containerId, filter);
                if (addresses?.Any() == true)
                {
                    result.AddRange(addresses);
                }
            }
            return result;
        }

        public List<ContainerID> ListContainers()
        {
            return metaBase.Containers();
        }

        public void ToMoveIt(Address address)
        {
            metaBase.MoveIt(address);
        }

        public void Put(FSObject obj)
        {
            if (useWriteCache)
            {
                writeCache.Put(obj);
            }
            else
            {
                var blobovniczaId = blobStor.Put(obj);
                if (blobovniczaId != null)
                {
                    metaBase.Put(obj, blobovniczaId);
                }
            }
        }

        public FSObject GetRange(ulong length, ulong offset, Address address)
        {
            var range = new Range()
            {
                Length = length,
                Offset = offset,
            };
            var obj = new FSObject();

            if (useWriteCache)
            {
                var small = writeCache.GetRangeSmall(address, range);
                if (small != null)
                {
                    obj.Payload = ByteString.CopyFrom(small);
                    return obj;
                }

                var big = writeCache.GetRangeBig(address, range);
                if (big != null)
                {
                    obj.Payload = ByteString.CopyFrom(big);
                    return obj;
                }
            }

            var isExist = metaBase.Exists(address);
            if (!isExist)
            {
                return null;
            }

            var blobovniczaID = metaBase.IsSmall(address);
            if (blobovniczaID != null)
            {
                var small = blobStor.GetRangeSmall(address, range, blobovniczaID);
                if (small != null)
                {
                    obj.Payload = ByteString.CopyFrom(small);
                    return obj;
                }
            }
            else
            {
                var big = blobStor.GetRangeBig(address, range);
                if (big != null)
                {
                    obj.Payload = ByteString.CopyFrom(big);
                    return obj;
                }
            }
            return null;
        }


        public List<Address> Select(ContainerID cid, SearchFilters filter)
        {
            return metaBase.Select(cid, filter);
        }
    }
}
