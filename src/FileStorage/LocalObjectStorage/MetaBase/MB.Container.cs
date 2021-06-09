
using System;
using System.Collections.Generic;
using Neo.FileStorage.API.Refs;
using Neo.IO.Data.LevelDB;

namespace Neo.FileStorage.LocalObjectStorage.MetaBase
{
    public sealed partial class MB
    {
        public List<ContainerID> Containers()
        {
            List<ContainerID> list = new();
            Iterate(ContainerPrefix, (key, value) =>
            {
                list.Add(ContainerID.FromSha256Bytes(key[1..]));
            });
            return list;
        }

        public ulong ContainerSize(ContainerID cid)
        {
            byte[] sizeb = db.Get(ReadOptions.Default, ContainerSizeKey(cid));
            if (sizeb is null) throw new KeyNotFoundException();
            return BitConverter.ToUInt64(sizeb);
        }

        private void ChangeContainerSize(ContainerID cid, ulong delta, bool increase)
        {
            ulong size;
            try
            {
                size = ContainerSize(cid);
                if (increase)
                    size += delta;
                else if (delta < size)
                    size -= delta;
                else
                    size = 0;
            }
            catch (KeyNotFoundException)
            {
                size = delta;
            }
            db.Put(WriteOptions.Default, ContainerSizeKey(cid), BitConverter.GetBytes(size));
        }
    }
}
