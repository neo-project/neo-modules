using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using System;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.LocalObjectStorage.Shards
{
    public sealed partial class WriteCache : IDisposable
    {
        public FSObject Get(Address address)
        {
            if (mem.TryGetValue(address, out ObjectInfo oi))
            {
                return oi.Object;
            }
            byte[] data = db.Get(address);
            if (data is not null)
            {
                flushed.TryGet(address, out _);
                return FSObject.Parser.ParseFrom(data);
            }
            try
            {
                data = fsTree.Get(address);
            }
            catch
            {
                throw new ObjectNotFoundException();
            }
            flushed.TryGet(address, out _);
            return FSObject.Parser.ParseFrom(data);
        }
    }
}
