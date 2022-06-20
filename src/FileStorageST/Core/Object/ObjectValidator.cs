using Neo.FileStorage.API.Lock;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.StorageGroup;
using Neo.FileStorage.API.Tombstone;
using System;
using System.Collections.Generic;
using System.Linq;
using static Neo.FileStorage.Storage.Helper;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.Core.Object
{
    public class ObjectValidator : IObjectValidator
    {
        public IObjectInhumer Inhumer { get; init; }
        public IObjectLocker Locker { get; init; }
        public IEpochSource EpochSource { get; init; }

        public VerifyResult Validate(FSObject obj)
        {
            if (obj is null)
                return VerifyResult.Null;
            try
            {
                ObjectIDCheck(obj.ObjectId);
            }
            catch
            {
                return VerifyResult.InvalidID;
            }
            if (obj.Header is null)
                return VerifyResult.NoHeader;
            try
            {
                ContainerIDCheck(obj.ContainerId);
            }
            catch
            {
                return VerifyResult.InvalidContainerID;
            }
            try
            {
                OwnerIDCheck(obj.OwnerId);
            }
            catch
            {
                return VerifyResult.InvalidOwnerID;
            }
            for (; obj is not null; obj = obj.Parent)
            {
                var r = CheckAttributes(obj);
                if (r != VerifyResult.Success) return r;
                if (!ValidateSignatureKey(obj)) return VerifyResult.InvalidKey;
                if (!CheckExpiration(obj)) return VerifyResult.Expiration;
                if (!obj.CheckVerificationFields()) return VerifyResult.InvalidSignature;
            }
            return VerifyResult.Success;
        }

        private VerifyResult CheckAttributes(FSObject obj)
        {
            HashSet<string> unique_attrs = new();
            foreach (var attr in obj.Attributes)
            {
                if (!unique_attrs.Add(attr.Key)) return VerifyResult.DuplicateAttribute;
                if (attr.Value == "") return VerifyResult.EmptyAttributeValue;
            }
            return VerifyResult.Success;
        }

        private bool ValidateSignatureKey(FSObject obj)
        {
            var token = obj.SessionToken;
            var key = obj.Signature.Key;

            if (token is null || !token.Body.SessionKey.Equals(key))
                return obj.OwnerId.Equals(OwnerID.FromPublicKey(key.ToByteArray()));

            // TODO: perform token verification
            return true;
        }

        public bool CheckExpiration(FSObject obj)
        {
            try
            {
                var expire = ExpirationEpochAttributeValue(obj);
                if (EpochSource.CurrentEpoch > expire) return false;
            }
            catch (InvalidOperationException)
            {
                Utility.Log(nameof(ObjectValidator), LogLevel.Debug, "object doesn't have expiration attribute");
            };
            return true;
        }

        private ulong ExpirationEpochAttributeValue(FSObject obj)
        {
            var expires = obj.Attributes.Where(p => p.Key == Header.Types.Attribute.SysAttributeExpEpoch).ToList();
            if (expires.Count == 0) throw new InvalidOperationException("no expiration attribute");
            return ulong.Parse(expires[0].Value);
        }

        public bool ValidateContent(FSObject obj)
        {
            switch (obj.ObjectType)
            {
                case ObjectType.Tombstone:
                    if (obj.Payload.Length == 0) return false;
                    Tombstone tombstone = Tombstone.Parser.ParseFrom(obj.Payload);
                    ulong exp;
                    try
                    {
                        exp = ExpirationEpochAttributeValue(obj);
                    }
                    catch (InvalidOperationException)
                    {
                        Utility.Log(nameof(ObjectValidator), LogLevel.Debug, "object doesn't have expiration attribute");
                        return false;
                    }
                    if (exp != tombstone.ExpirationEpoch)
                        return false;
                    var cid = obj.ContainerId;
                    var ids = tombstone.Members;
                    var addresses = new Address[ids.Count];
                    for (int i = 0; i < ids.Count; i++)
                    {
                        if (ids[i] is null) return false;
                        Address address = new(cid, ids[i]);
                        addresses[i] = address;
                    }
                    if (Inhumer != null)
                        Inhumer.Inhume(obj.Address, addresses);
                    break;
                case ObjectType.StorageGroup:
                    if (obj.Payload.Length == 0) return false;
                    StorageGroup sg = StorageGroup.Parser.ParseFrom(obj.Payload);
                    foreach (var id in sg.Members)
                    {
                        if (id is null) return false;
                    }
                    break;
                case ObjectType.Lock:
                    if (obj.Payload.Length == 0) return false;
                    if (obj.ContainerId is null) return false;
                    if (obj.ObjectId is null) return false;
                    var lockObj = Lock.Parser.ParseFrom(obj.Payload);
                    Locker.Lock(obj.ContainerId, obj.ObjectId, lockObj.Members.ToArray());
                    break;
            }
            return true;
        }
    }
}
