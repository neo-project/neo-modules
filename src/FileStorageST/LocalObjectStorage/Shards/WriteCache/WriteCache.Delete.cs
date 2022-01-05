using Neo.FileStorage.API.Refs;
using System;

namespace Neo.FileStorage.Storage.LocalObjectStorage.Shards
{
    public sealed partial class WriteCache : IDisposable
    {
        public void Delete(Address address)
        {
            string saddress = address.String();
            byte[] key = Utility.StrictUTF8.GetBytes(saddress);
            if (mem.TryRemove(saddress, out var oi))
            {
                lock (memorySizeLocker)
                {
                    memorySize -= (ulong)oi.Data.Length;
                }
                Utility.Log(nameof(WriteCache), LogLevel.Debug, $"in-mem DELETE, address={saddress}");
                return;
            }
            if (db.Contains(key))
            {
                var length = db.Get(key).Length;
                db.Delete(key);
                objCounters.DecSmallCount();
                Utility.Log(nameof(WriteCache), LogLevel.Debug, $"db DELETE, address={saddress}");
                return;
            }
            try
            {
                fsTree.Delete(address);
                objCounters.DecBigCount();
                Utility.Log(nameof(WriteCache), LogLevel.Debug, $"fstree DELETE, address={saddress}");
            }
            catch (Exception e)
            {
                Utility.Log(nameof(WriteCache), LogLevel.Debug, $"can't delete from write cache, address={saddress}, error={e.Message}");
            }
        }
    }
}
