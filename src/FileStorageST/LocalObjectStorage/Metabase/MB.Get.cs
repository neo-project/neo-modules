using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using System.Linq;
using static Neo.FileStorage.Storage.LocalObjectStorage.Metabase.Helper;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.LocalObjectStorage.Metabase
{
    public sealed partial class MB
    {
        public FSObject Get(Address address, bool raw = false)
        {
            return Get(address, true, raw);
        }

        private FSObject Get(Address address, bool check_grave_yard, bool raw)
        {
            if (check_grave_yard)
            {
                switch (InGraveYard(address))
                {
                    case GraveYardState.GCMark:
                        throw new ObjectNotFoundException();
                    case GraveYardState.Tombstone:
                        throw new ObjectAlreadyRemovedException();
                }
            }
            FSObject obj = GetObject(Primarykey(address));
            if (obj is not null) return obj;
            obj = GetObject(TombstoneKey(address));
            if (obj is not null) return obj;
            obj = GetObject(StorageGroupKey(address));
            if (obj is not null) return obj;
            return GetVirtualObject(address, raw);
        }

        private FSObject GetObject(byte[] key)
        {
            byte[] data = db.Get(key);
            if (data is null) return null;
            return FSObject.Parser.ParseFrom(data);
        }

        private FSObject GetVirtualObject(Address address, bool raw)
        {
            if (raw)
                throw new SplitInfoException(GetSplitInfo(address));
            var data = db.Get(ParentKey(address.ContainerId, address.ObjectId));
            if (data is null) throw new ObjectNotFoundException();
            var children = DecodeObjectIDList(data);
            if (children.Count == 0) throw new ObjectNotFoundException();
            var child = children[^1];
            var obj = GetObject(Primarykey(new()
            {
                ContainerId = address.ContainerId,
                ObjectId = child,
            }));
            if (obj.Parent is null)
                throw new ObjectNotFoundException();
            return obj.Parent;
        }

        private SplitInfo GetSplitInfo(Address address)
        {
            byte[] data = db.Get(RootKey(address));
            if (data is null) throw new ObjectNotFoundException();
            return SplitInfo.Parser.ParseFrom(data);
        }
    }
}
