
using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Neo.FileStorage.API.Refs;

namespace Neo.FileStorage.Storage.LocalObjectStorage.Metabase;

public sealed partial class MB
{
    public void Lock(ContainerID cid, ObjectID locker, params ObjectID[] locked)
    {
        if (locked.Length == 0)
            throw new InvalidOperationException("empty locked list");
        var nlocked = locked.Distinct().OrderBy(p => p).ToList();
        var address = new Address { ContainerId = cid };
        foreach (var oid in nlocked)
        {
            address.ObjectId = oid;
            var data = db.Get(Primarykey(address));
            if (data is null)
                throw new InvalidOperationException("lock non regular object");
        }
        foreach (var oid in nlocked)
        {
            address.ObjectId = oid;
            var key = LockedKey(address);
            var data = db.Get(key);
            var lockers = new List<ObjectID>();
            if (data is not null)
            {
                lockers = Helper.DecodeObjectIDList(data);
                if (lockers.Contains(oid)) continue;
            }
            lockers.Add(locker);
            db.Put(key, Helper.EncodeObjectIDList(lockers));
        }
    }

    public bool IsLocked(ContainerID cid, ObjectID oid)
    {
        var data = db.Get(LockedKey(new Address { ContainerId = cid, ObjectId = oid }));
        return data is not null;
    }

    public void FreeLockBy(params Address[] lockers)
    {
        foreach (var locker in lockers)
        {
            var address = new Address { ContainerId = locker.ContainerId, ObjectId = new() { Value = ByteString.Empty } };
            db.Iterate(LockedKey(address), (key, value) =>
            {
                var li = Helper.DecodeObjectIDList(value);
                if (li.Contains(locker.ObjectId))
                {
                    li.Remove(locker.ObjectId);
                    if (li.Count == 0)
                        db.Delete(key);
                    else
                        db.Put(key, Helper.EncodeObjectIDList(li));
                }
                return false;
            });
        }
    }
}
