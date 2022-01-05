using Neo.FileStorage.API.Refs;
using System;
using System.Collections.Generic;

namespace Neo.FileStorage.Storage.LocalObjectStorage.Metabase
{
    public sealed partial class MB
    {
        public HashSet<ContainerID> Containers()
        {
            HashSet<ContainerID> cids = new();
            db.Iterate(PrimaryPrefix, (key, _) =>
            {
                var address = ParsePrimaryKey(key);
                cids.Add(address.ContainerId);
                return false;
            });
            db.Iterate(TombstonePrefix, (key, _) =>
            {
                var address = ParseTombstoneKey(key);
                cids.Add(address.ContainerId);
                return false;
            });
            db.Iterate(StorageGroupPrefix, (key, _) =>
            {
                var address = ParseStorageGroupKey(key);
                cids.Add(address.ContainerId);
                return false;
            });
            return cids;
        }

        public ulong ContainerSize(ContainerID cid)
        {
            byte[] sizeb = db.Get(ContainerSizeKey(cid));
            if (sizeb is null) return 0;
            return BitConverter.ToUInt64(sizeb);
        }

        private void ChangeContainerSize(ContainerID cid, ulong delta, bool increase)
        {
            ulong size;
            size = ContainerSize(cid);
            if (increase)
                size += delta;
            else if (delta < size)
                size -= delta;
            else
                size = 0;
            Utility.Log(nameof(Metabase), LogLevel.Debug, $"local container size, cid={cid.String()}, size={size}");
            db.Put(ContainerSizeKey(cid), BitConverter.GetBytes(size));
        }
    }
}
