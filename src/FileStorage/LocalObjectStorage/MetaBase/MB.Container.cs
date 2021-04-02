
using Neo.FileStorage.API.Refs;
using Neo.IO.Data.LevelDB;
using Neo.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using static Neo.Helper;

namespace Neo.FileStorage.LocalObjectStorage.MetaBase
{
    public sealed partial class MB
    {
        public List<ContainerID> Containers()
        {
            return db.Seek(ReadOptions.Default, ContainerPrefix, SeekDirection.Forward, (key, value) =>
            {
                return ContainerID.FromSha256Bytes(key[1..]);
            }).ToList();
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
