using Google.Protobuf;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.Object;
using Neo.IO.Data.LevelDB;
using System.Collections.Generic;
using System.IO;
using FSObject = Neo.FileStorage.API.Object.Object;
using Neo.IO;
using System;

namespace Neo.FileStorage.LocalObjectStorage.MetaBase
{
    public sealed partial class MB
    {
        private bool IsGraveYard(Address address)
        {
            return db.Get(ReadOptions.Default, GraveYardKey(address)) is not null;
        }

        public FSObject Get(Address address, bool raw)
        {
            return Get(address, true, raw);
        }

        private FSObject Get(Address address, bool check_grave_yard, bool raw)
        {
            if (check_grave_yard && IsGraveYard(address))
                throw new ObjectAlreadyRemovedException();
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
            byte[] data = db.Get(ReadOptions.Default, key);
            if (data is null) return null;
            return FSObject.Parser.ParseFrom(data);
        }

        private FSObject GetVirtualObject(Address address, bool raw)
        {
            if (raw)
                throw new SplitInfoException(GetSplitInfo(address));
            var children = GetChildren(address);
            if (children.Count == 0) throw new ObjectNotFoundException();
            var child = GetObject(Primarykey(children[^1]));
            if (child is null) throw new InvalidOperationException("can't get child with parent");
            if (child.Parent is null) throw new ObjectNotFoundException();
            return child.Parent;
        }

        private SplitInfo GetSplitInfo(Address address)
        {
            byte[] data = db.Get(ReadOptions.Default, RootKey(address));
            if (data is null) return null;
            return SplitInfo.Parser.ParseFrom(data);
        }

        private List<Address> GetChildren(Address address)
        {
            byte[] data = db.Get(ReadOptions.Default, ParentKey(address));
            if (data is null) return null;
            List<Address> children = new ();
            using MemoryStream ms = new (data);
            using BinaryReader reader = new (ms);
            int count = (int)reader.ReadVarInt(int.MaxValue);
            for (int i = 0; i < count; i++)
            {
                Address addr = new ()
                {
                    ContainerId = new ()
                    {
                        Value = ByteString.CopyFrom(reader.ReadBytes(32)),
                    },
                    ObjectId = new ()
                    {
                        Value = ByteString.CopyFrom(reader.ReadBytes(32)),
                    },
                };
                children.Add(addr);
            }
            return children;
        }

        public bool Exists(Address address)
        {
            if (IsGraveYard(address))
                throw new ObjectAlreadyRemovedException();
            if (InBucket(Primarykey(address))) return true;
            if (InBucket(ParentKey(address)))
                throw new SplitInfoException(GetSplitInfo(address));
            if (InBucket(TombstoneKey(address))) return true;
            return InBucket(StorageGroupKey(address));
        }

        private bool InBucket(byte[] key)
        {
            return db.Get(ReadOptions.Default, key) is not null;
        }
    }
}
