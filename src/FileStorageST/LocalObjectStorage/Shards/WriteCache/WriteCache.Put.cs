using Google.Protobuf;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Cache;
using Neo.FileStorage.Database;
using Neo.FileStorage.Database.LevelDB;
using Neo.FileStorage.Storage.LocalObjectStorage.Blob;
using Neo.FileStorage.Storage.LocalObjectStorage.Blobstor;
using Neo.FileStorage.Storage.LocalObjectStorage.Metabase;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.LocalObjectStorage.Shards
{
    public sealed partial class WriteCache : IDisposable
    {
        public void Put(FSObject obj)
        {
            ObjectInfo oi = new()
            {
                Object = obj,
                SAddress = obj.Address.String(),
                Data = obj.ToByteArray()
            };
            var len = (ulong)oi.Data.Length;
            if (MaxObjectSize < len)
                throw new InvalidOperationException("Object too big");
            if (len < SmallObjectSize && memorySize + len <= MaxMemorySize)
            {
                Interlocked.Add(ref memorySize, len);
                mem[obj.Address.String()] = oi;
                Utility.Log(nameof(WriteCache), LogLevel.Debug, $"in-mem PUT, address={obj.Address.String()}");
                return;
            }
            if (len <= SmallObjectSize)
                PersistSmallObjects(oi);
            else
                PersistBigObject(oi);
        }
    }
}
