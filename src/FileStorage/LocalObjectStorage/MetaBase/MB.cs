using Google.Protobuf;
using Neo.IO.Data.LevelDB;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using System;
using System.Collections.Generic;
using static Neo.FileStorage.API.Object.SearchRequest.Types.Body.Types;
using static Neo.Helper;
using static Neo.Utility;

namespace Neo.FileStorage.LocalObjectStorage.MetaBase
{
    public sealed partial class MB : IDisposable
    {
        private readonly byte[] ContainerPrefix = new byte[] { 0x00 };
        private readonly byte[] ObjectPrefix = new byte[] { 0x01 };
        private readonly byte[] GraveYardPrefix = new byte[] { 0x02 };
        private readonly byte[] TombstonePrefix = new byte[] { 0x03 };
        private readonly byte[] StorageGroupPrefix = new byte[] { 0x04 };
        private readonly byte[] RootPrefix = new byte[] { 0x05 };
        private readonly byte[] ParentPrefix = new byte[] { 0x06 };
        private readonly byte[] SmallPrefix = new byte[] { 0x07 };
        private readonly byte[] ToMoveItPrefix = new byte[] { 0x08 };
        private readonly byte[] PayloadHashPrefix = new byte[] { 0x09 };
        private readonly byte[] SplitPrefix = new byte[] { 0x0a };
        private readonly byte[] OwnerPrefix = new byte[] { 0x0b };
        private readonly byte[] AttributePrefix = new byte[] { 0x0c };
        private readonly byte[] ZeroValue = new byte[] { 0xFF };
        private readonly string path;
        private readonly DB db;
        private readonly Dictionary<MatchType, Func<string, byte[], string, bool>> matchers;

        public MB(string p)
        {
            path = p;
            matchers = new ()
            {
                { MatchType.Unspecified, UnknownMatcher },
                { MatchType.StringEqual, StringEqualMatcher },
                { MatchType.StringNotEqual, StringNotEqualMatcher }
            };
            db = DB.Open(path);
        }

        public void Dispose()
        {
            db.Dispose();
        }

        private string StringifyValue(string key, byte[] value)
        {
            return key switch
            {
                Filter.FilterHeaderPayloadHash or Filter.FilterHeaderHomomorphicHash => value.ToHexString(),
                Filter.FilterHeaderCreationEpoch or Filter.FilterHeaderPayloadLength => BitConverter.ToUInt64(value).ToString(),
                _ => StrictUTF8.GetString(value),
            };
        }

        private bool UnknownMatcher(string key, byte[] value, string filter)
        {
            return false;
        }

        private bool StringEqualMatcher(string key, byte[] value, string filter)
        {
            return StringifyValue(key, value) == filter;
        }

        private bool StringNotEqualMatcher(string key, byte[] value, string filter)
        {
            return StringifyValue(key, value) != filter;
        }

        private byte[] BucketKeyHelper(string header, string value)
        {
            switch (header)
            {
                case Filter.FilterHeaderPayloadHash:
                    return value.HexToBytes();
                case Filter.FilterHeaderSplitID:
                    SplitID s = new ();
                    if (s.Parse(value)) return s.ToBytes();
                    return null;
                default:
                    return StrictUTF8.GetBytes(value);
            }
        }

        private byte[] ContainerSizeKey(ContainerID cid)
        {
            if (cid is null) throw new ArgumentNullException(nameof(cid));
            return Concat(ContainerPrefix, cid.Value.ToByteArray());
        }

        private byte[] GraveYardKey(Address address)
        {
            if (address is null) throw new ArgumentNullException(nameof(address));
            return Concat(GraveYardPrefix, address.ContainerId.Value.ToByteArray(), address.ObjectId.Value.ToByteArray());
        }

        private byte[] Primarykey(Address address)
        {
            if (address is null) throw new ArgumentNullException(nameof(address));
            return Concat(ObjectPrefix, address.ContainerId.Value.ToByteArray(), address.ObjectId.Value.ToByteArray());
        }

        private byte[] TombstoneKey(Address address)
        {
            if (address is null) throw new ArgumentNullException(nameof(address));
            return Concat(TombstonePrefix, address.ContainerId.Value.ToByteArray(), address.ObjectId.Value.ToByteArray());
        }

        private byte[] StorageGroupKey(Address address)
        {
            if (address is null) throw new ArgumentNullException(nameof(address));
            return Concat(StorageGroupPrefix, address.ContainerId.Value.ToByteArray(), address.ObjectId.Value.ToByteArray());
        }

        private byte[] RootKey(Address address)
        {
            if (address is null) throw new ArgumentNullException(nameof(address));
            return Concat(RootPrefix, address.ContainerId.Value.ToByteArray(), address.ObjectId.Value.ToByteArray());
        }

        private byte[] ParentKey(Address address)
        {
            if (address is null) throw new ArgumentNullException(nameof(address));
            return Concat(ParentPrefix, address.ContainerId.Value.ToByteArray(), address.ObjectId.Value.ToByteArray());
        }

        private byte[] SmallKey(Address address)
        {
            if (address is null) throw new ArgumentNullException(nameof(address));
            return Concat(SmallPrefix, address.ContainerId.Value.ToByteArray(), address.ObjectId.Value.ToByteArray());
        }

        private byte[] ToMoveItKey(Address address)
        {
            if (address is null) throw new ArgumentNullException(nameof(address));
            return Concat(ToMoveItPrefix, address.ContainerId.Value.ToByteArray(), address.ObjectId.Value.ToByteArray());
        }

        private byte[] PayloadHashKey(Address address, Checksum checksum)
        {
            if (address is null || checksum is null) throw new ArgumentNullException();
            return Concat(PayloadHashPrefix, address.ContainerId.Value.ToByteArray(), checksum.Sum.ToByteArray(), address.ObjectId.Value.ToByteArray());
        }

        private byte[] SplitKey(Address address, SplitID sid)
        {
            if (address is null || sid is null) throw new ArgumentNullException();
            return Concat(SplitPrefix, address.ContainerId.Value.ToByteArray(), sid.ToBytes(), address.ObjectId.Value.ToByteArray());
        }

        private byte[] OwnerKey(Address address, OwnerID wid)
        {
            if (address is null || wid is null) throw new ArgumentNullException();
            return Concat(OwnerPrefix, address.ContainerId.Value.ToByteArray(), wid.Value.ToByteArray(), address.ObjectId.ToByteArray());
        }

        private byte[] AttributeKey(Address address, Header.Types.Attribute attr)
        {
            if (address is null || attr is null) throw new ArgumentNullException();
            return Concat(AttributePrefix, address.ContainerId.Value.ToByteArray(), StrictUTF8.GetBytes(attr.Key), StrictUTF8.GetBytes(attr.Value), address.ObjectId.Value.ToByteArray());
        }
    }
}
