using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using System;
using System.Collections.Generic;
using System.Linq;
using static Neo.Utility;

namespace Neo.FileStorage.Storage.LocalObjectStorage.Metabase
{
    public sealed partial class MB
    {
        public void IterateGraveYard(Func<Grave, bool> handler)
        {
            db.Iterate(GraveYardPrefix, (key, value) =>
            {
                return handler(new()
                {
                    GCMark = value.SequenceEqual(GCMARK),
                    Address = ParseGraveYardKey(key)
                });
            });
        }

        public void IterateExpired(ulong epoch, Action<ObjectType, Address> handler)
        {
            byte[] expired_epoch_key = StrictUTF8.GetBytes(Header.Types.Attribute.SysAttributeExpEpoch);
            db.Iterate(AttributePrefix, (key, value) =>
            {
                ParseAttributeKey(key, out var address, out var attribute);
                if (!attribute.AsSpan().StartsWith(expired_epoch_key)) return false;
                ulong expired_epoch_value = ulong.Parse(StrictUTF8.GetString(attribute[expired_epoch_key.Length..]));
                if (epoch <= expired_epoch_value) return false;
                handler(GetObjectType(address), address);
                return false;
            });
        }

        private ObjectType GetObjectType(Address address)
        {
            if (InBucket(TombstoneKey(address))) return ObjectType.Tombstone;
            if (InBucket(StorageGroupKey(address))) return ObjectType.StorageGroup;
            return ObjectType.Regular;
        }

        public void IterateCoveredByTombstones(HashSet<Address> tss, Action<Address> func)
        {
            db.Iterate(GraveYardPrefix, (key, value) =>
            {
                if (value.SequenceEqual(GCMARK))
                    return false;
                var ts = ParseGraveYardKey(value);
                if (tss.Contains(ts))
                {
                    func(ParseGraveYardKey(key));
                }
                return false;
            });
        }
    }
}
