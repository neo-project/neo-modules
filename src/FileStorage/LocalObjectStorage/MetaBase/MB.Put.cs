using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.LocalObjectStorage.Blob;
using Neo.IO.Data.LevelDB;
using static Neo.FileStorage.LocalObjectStorage.MetaBase.Helper;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.LocalObjectStorage.MetaBase
{
    public sealed partial class MB
    {
        public void Put(FSObject obj, BlobovniczaID bid, SplitInfo si = null)
        {
            bool is_parent = si is not null;
            bool exist;
            try
            {
                exist = Exists(obj.Address);
            }
            catch (SplitInfoException)
            {
                exist = true;
            }
            if (exist)
            {
                if (!is_parent && bid is not null)
                {
                    UpdateBlobovniczaID(obj.Address, bid);
                    return;
                }
                if (is_parent)
                {
                    UpdateSplitInfo(obj.Address, si);
                    return;
                }
                return;
            }
            if (obj.Parent is not null && !is_parent)
            {
                SplitInfo psi = SplitInfoFromObject(obj);
                Put(obj.Parent, bid, psi);
            }
            foreach (var item in UniqueIndexes(obj, si, bid))
            {
                db.Put(WriteOptions.Default, item.Item1, item.Item2);
            }
            foreach (var item in ListIndexes(obj))
            {
                List<ObjectID> list;
                var data = db.Get(ReadOptions.Default, item.Item1);
                if (data is null)
                    list = new();
                else
                    list = DecodeObjectIDList(data);
                list.Add(item.Item2);
                db.Put(WriteOptions.Default, item.Item1, EncodeObjectIDList(list));
            }
            foreach (var key in FakeBucketTreeIndexes(obj))
            {
                db.Put(WriteOptions.Default, key, ZeroValue);
            }
            if (obj.ObjectType == ObjectType.Regular && !is_parent)
            {
                ChangeContainerSize(obj.ContainerId, obj.PayloadSize, true);
            }
        }

        private void UpdateBlobovniczaID(Address address, BlobovniczaID bid)
        {
            byte[] key = SmallKey(address);
            if (db.Get(ReadOptions.Default, key) is null)
                throw new InvalidOperationException("updating blobovnicza id on object without it");
            db.Put(WriteOptions.Default, SmallKey(address), bid);
        }

        private void UpdateSplitInfo(Address address, SplitInfo si)
        {
            byte[] key = RootKey(address);
            byte[] old = db.Get(ReadOptions.Default, key);
            if (old is null)
                throw new InvalidOperationException("updating split info on object without it");
            SplitInfo osi = SplitInfo.Parser.ParseFrom(old);
            si = MergeSplitInfo(osi, si);
            db.Put(WriteOptions.Default, key, si.ToByteArray());
        }

        private SplitInfo MergeSplitInfo(SplitInfo from, SplitInfo to)
        {
            to.SplitId = from.SplitId;
            if (from.LastPart is not null) to.LastPart = from.LastPart;
            if (from.Link is not null) to.Link = from.Link;
            return to;
        }

        private SplitInfo SplitInfoFromObject(FSObject obj)
        {
            if (obj.Parent is null) return null;
            SplitInfo result = new();
            result.SplitId = obj.SplitId.ToByteString();
            if (IsLinkObject(obj))
                result.Link = obj.ObjectId;
            else if (IsLastObject(obj))
                result.LastPart = obj.ObjectId;
            else
                throw new InvalidOperationException("invalid root object");
            return result;
        }

        private bool IsLinkObject(FSObject obj)
        {
            return obj.Children.Any() && obj.Parent is not null;
        }

        private bool IsLastObject(FSObject obj)
        {
            return !obj.Children.Any() && obj.Parent is not null;
        }

        private List<(byte[], byte[])> UniqueIndexes(FSObject obj, SplitInfo si, BlobovniczaID bid)
        {
            List<(byte[], byte[])> result = new();
            if (si is null)
            {
                switch (obj.ObjectType)
                {
                    case ObjectType.Regular:
                        result.Add((Primarykey(obj.Address), obj.CutPayload().ToByteArray()));
                        break;
                    case ObjectType.Tombstone:
                        result.Add((TombstoneKey(obj.Address), obj.CutPayload().ToByteArray()));
                        break;
                    case ObjectType.StorageGroup:
                        result.Add((StorageGroupKey(obj.Address), obj.CutPayload().ToByteArray()));
                        break;
                    default:
                        throw new UnknownObjectTypeException();
                }
                if (bid is not null)
                    result.Add((SmallKey(obj.Address), bid));
            }
            if (obj.ObjectType == ObjectType.Regular && !obj.HasParent)
            {
                byte[] splitInfo = Array.Empty<byte>();
                if (si is not null) splitInfo = si.ToByteArray();
                result.Add((RootKey(obj.Address), splitInfo));
            }
            return result;
        }
    }
}
