using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.IO.Data.LevelDB;
using System;
using System.Collections.Generic;
using System.Linq;
using static Neo.Utility;

namespace Neo.FileStorage.LocalObjectStorage.MetaBase
{
    public sealed partial class MB
    {
        public void IterateGraveYard(Action<Grave> func)
        {
            using Iterator it = db.NewIterator(ReadOptions.Default);
            for (it.Seek(GraveYardPrefix); it.Valid(); it.Next())
            {
                if (!it.Key().AsSpan().StartsWith(GraveYardPrefix)) break;
                func(new()
                {
                    GCMark = it.Value().SequenceEqual(InhumeGCMarkValue),
                    Address = Address.Parser.ParseFrom(it.Key()[1..]),
                });
            }
        }

        private void Iterate(byte[] prefix, Action<byte[], byte[]> func)
        {
            using Iterator it = db.NewIterator(ReadOptions.Default);
            for (it.Seek(prefix); it.Valid(); it.Next())
            {
                if (!it.Key().AsSpan().StartsWith(prefix)) break;
                func(it.Key(), it.Value());
            }
        }

        public void IterateExpired(ulong epoch, Action<ObjectType, Address> func)
        {
            byte[] expired_epoch_key = StrictUTF8.GetBytes(Header.Types.Attribute.SysAttributeExpEpoch);
            Iterate(AttributePrefix, (key, value) =>
            {
                key = key[1..];
                byte[] cid = key[..32];
                key = key[32..];
                if (!key.AsSpan().StartsWith(expired_epoch_key)) return;
                key = key[expired_epoch_key.Length..];
                ulong expired_epoch_value = ulong.Parse(StrictUTF8.GetString(key[..^32]));
                if (epoch < expired_epoch_value) return;
                byte[] oid = key[^32..];
                Address address = new()
                {
                    ContainerId = ContainerID.FromSha256Bytes(cid),
                    ObjectId = ObjectID.FromSha256Bytes(oid),
                };
                func(GetObjectType(address), address);
            });
        }

        private ObjectType GetObjectType(Address address)
        {
            if (InBucket(TombstoneKey(address))) return ObjectType.Tombstone;
            if (InBucket(StorageGroupKey(address))) return ObjectType.StorageGroup;
            return ObjectType.Regular;
        }

        public void IterateCoveredByTombstones(HashSet<string> tss, Action<Address> func)
        {
            Iterate(GraveYardPrefix, (key, value) =>
            {
                if (tss.Contains(StrictUTF8.GetString(value)))
                {
                    func(new(ContainerID.FromSha256Bytes(key[1..^32]), ObjectID.FromSha256Bytes(key[^32..])));
                }
            });
        }
    }
}
