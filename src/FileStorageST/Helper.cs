using Google.Protobuf;
using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Session;
using Neo.FileStorage.API.Container;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using System;
using FSRange = Neo.FileStorage.API.Object.Range;

namespace Neo.FileStorage.Storage
{
    public static class Helper
    {
        public static ByteString Concat(this ByteString a, ByteString b)
        {
            if (a is null)
                throw new ArgumentNullException(nameof(a));
            if (a is null)
                throw new ArgumentNullException(nameof(b));
            return ByteString.CopyFrom(Neo.Helper.Concat(a.ToByteArray(), b.ToByteArray()));
        }

        public static ByteString Range(this ByteString a, ulong left, ulong right)
        {
            if (a is null)
                throw new ArgumentNullException(nameof(a));
            return ByteString.CopyFrom(a.ToByteArray()[(int)left..(int)right]);
        }

        public static SessionToken OriginalSessionToken(RequestMetaHeader meta)
        {
            while (meta.Origin is not null)
                meta = meta.Origin;
            return meta.SessionToken;
        }

        public static BearerToken OriginalBearerToken(RequestMetaHeader meta)
        {
            while (meta.Origin is not null)
                meta = meta.Origin;
            return meta.BearerToken;
        }

        public static void ObjectIDCheck(ObjectID oid)
        {
            if (oid is null) throw new InvalidOperationException("object id is missing");
            if (oid.Value.Length != ObjectID.ValueSize)
                throw new InvalidOperationException("invalid object id");
        }

        public static void ContainerIDCheck(ContainerID cid)
        {
            if (cid is null) throw new InvalidOperationException("container id is missing");
            if (cid.Value.Length != ContainerID.ValueSize)
                throw new InvalidOperationException("invalid container id");
        }

        public static void AddressCheck(Address address)
        {
            if (address is null) throw new InvalidOperationException("object address is missing");
            ContainerIDCheck(address.ContainerId);
            ObjectIDCheck(address.ObjectId);
        }

        public static void RangeCheck(FSRange range)
        {
            if (range is null) throw new InvalidOperationException("object range is missing");
        }

        public static void ChecsumTypeCheck(ChecksumType type)
        {
            if (type == ChecksumType.Unspecified) throw new InvalidOperationException("checksum type unspecified");
            if (type != ChecksumType.Sha256 && type != ChecksumType.Tz)
                throw new InvalidOperationException("unsupport checksum type");
        }

        public static void SignatureCheck(Signature sig)
        {
            if (sig is null) throw new InvalidOperationException("signature missing");
            if (sig.Key is null || sig.Sign is null) throw new InvalidOperationException("invalid signature");
        }

        public static void ContainerCheck(Container container)
        {
            if (container is null) throw new InvalidOperationException("container is missing");
            if (container.PlacementPolicy is null) throw new InvalidOperationException("placement policy is missing");
            OwnerIDCheck(container.OwnerId);
        }

        public static void OwnerIDCheck(OwnerID owner)
        {
            if (owner is null) throw new InvalidOperationException("owner id is missing");
            if (owner.Value.Length != OwnerID.ValueSize) throw new InvalidOperationException("invalid owner id");
        }

        public static void EACLCheck(EACLTable eacl)
        {
            if (eacl is null) throw new InvalidOperationException("eacl is missing");
            ContainerIDCheck(eacl.ContainerId);
        }

        public static void MergeSplitInfo(this SplitInfo to, SplitInfo from)
        {
            to.SplitId = from.SplitId;
            if (from.LastPart is not null)
                to.LastPart = from.LastPart;
            if (from.Link is not null)
                to.Link = from.Link;
        }
    }
}
