using System;
using System.Collections.Generic;
using System.Linq;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.StorageGroup;
using Neo.FileStorage.API.Tombstone;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.Core.Object
{
    public class ObjectValidator
    {
        public IObjectDeleteHandler DeleteHandler { get; init; }
        public IEpochSource EpochSource { get; init; }

        public VerifyResult Validate(FSObject obj)
        {
            if (obj is null)
                return VerifyResult.Null;
            else if (obj.ObjectId is null)
                return VerifyResult.NoID;
            else if (obj.Header is null)
                return VerifyResult.NoHeader;
            else if (obj.Header.ContainerId is null)
                return VerifyResult.NoContainerID;

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
                return obj.OwnerId.Equals(OwnerID.FromScriptHash(key.ToByteArray().PublicKeyToScriptHash()));

            // TODO: perform token verification
            return true;
        }

        public bool CheckExpiration(FSObject obj)
        {
            try
            {
                var expire = ExpirationEpochAttributeValue(obj);
                if (EpochSource.CurrentEpoch < expire) return false;
            }
            catch (InvalidOperationException e)
            {
                Utility.Log(nameof(ObjectValidator), LogLevel.Warning, e.Message);
            };
            return true;
        }

        private ulong ExpirationEpochAttributeValue(FSObject obj)
        {
            var expires = obj.Attributes.Where(p => p.Key == Header.Types.Attribute.SysAttributeExpEpoch).ToList();
            if (!expires.Any()) throw new InvalidOperationException();
            return ulong.Parse(expires[0].Value);
        }

        public bool ValidateContent(FSObject obj)
        {
            switch (obj.ObjectType)
            {
                case ObjectType.Tombstone:
                    if (!obj.Payload.Any()) return false;
                    Tombstone tombstone = Tombstone.Parser.ParseFrom(obj.Payload);
                    ulong exp;
                    try
                    {
                        exp = ExpirationEpochAttributeValue(obj);
                    }
                    catch (Exception e)
                    {
                        Utility.Log(nameof(ObjectValidator), LogLevel.Warning, e.Message);
                        return false;
                    }
                    if (exp != tombstone.ExpirationEpoch)
                        return false;
                    var cid = obj.ContainerId;
                    var id_list = tombstone.Members.ToList();
                    List<Address> address_list = new();
                    address_list.Add(obj.Address);
                    foreach (var id in id_list)
                    {
                        if (id is null) return false;
                        Address address = new(cid, id);
                        address_list.Add(address);
                    }
                    if (DeleteHandler != null)
                        DeleteHandler.DeleteObjects(address_list.ToArray());
                    break;
                case ObjectType.StorageGroup:
                    if (!obj.Payload.Any()) return false;
                    StorageGroup sg = StorageGroup.Parser.ParseFrom(obj.Payload.ToByteArray());
                    foreach (var id in sg.Members)
                    {
                        if (id is null) return false;
                    }
                    break;
            }
            return true;
        }
    }
}
