using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using System.Collections.Generic;
using System.Linq;
using static Neo.Helper;

namespace Neo.FileStorage.Storage.LocalObjectStorage.Metabase
{
    public sealed partial class MB
    {
        private enum GraveYardState : byte
        {
            Unkown,
            GCMark,
            Tombstone
        }

        private GraveYardState InGraveYard(Address address)
        {
            var val = db.Get(GraveYardKey(address));
            if (val is null) return GraveYardState.Unkown;
            if (val.SequenceEqual(GCMARK)) return GraveYardState.GCMark;
            return GraveYardState.Tombstone;
        }

        public bool Exists(Address address)
        {
            switch (InGraveYard(address))
            {
                case GraveYardState.GCMark:
                    throw new ObjectNotFoundException();
                case GraveYardState.Tombstone:
                    throw new ObjectAlreadyRemovedException();
            }
            if (InBucket(Primarykey(address))) return true;
            List<byte[]> keys = new();
            db.Iterate(Concat(ParentPrefix, address.ContainerId.Value.ToByteArray(), address.ObjectId.Value.ToByteArray()),
                (key, _) =>
                {
                    keys.Add(key);
                    return false;
                });
            if (keys.Count > 0)
                throw new SplitInfoException(GetSplitInfo(address));
            if (InBucket(TombstoneKey(address))) return true;
            return InBucket(StorageGroupKey(address));
        }

        private bool InBucket(byte[] key)
        {
            return db.Get(key) is not null;
        }
    }
}
