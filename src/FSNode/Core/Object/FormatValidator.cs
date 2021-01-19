using Google.Protobuf;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.SmartContract;
using NeoFS.API.v2.Object;
using NeoFS.API.v2.Refs;
using System;
using V2Object = NeoFS.API.v2.Object.Object;

namespace Neo.FSNode.Core.Object
{
    public interface IDeleteHandler
    {
        void DeleteObjects(params Address[] objAddrs);
    }

    public class FormatValidator
    {
        private IDeleteHandler deleteHandler;

        public FormatValidator()
        {

        }

        public bool Validate(V2Object obj)
        {
            if (obj is null)
                return false;
            else if (obj.ObjectId is null)
                return false;
            else if (obj.Header is null || obj.Header.ContainerId is null)
                return false;

            while (obj != null)
            {
                obj = obj.Parent();
                if (!ValidateSignatureKey(obj)) return false;
                if (!obj.VerifyIDSignature()) return false;
                if (!obj.VerifyID()) return false;
            }
            return true;
        }

        private bool ValidateSignatureKey(V2Object obj)
        {
            var token = obj.Header.SessionToken;
            var key = obj.Signature.Key;

            if (token is null || !token.Body.SessionKey.Equals(key))
                return CheckOwnerKey(obj.Header.OwnerId, obj.Signature.Key.ToByteArray());

            // TODO: perform token verification
            return true;
        }

        private bool CheckOwnerKey(OwnerID id, byte[] key)
        {
            var pubKey = ECPoint.FromBytes(key, ECCurve.Secp256r1);
            var scriptHash = pubKey.EncodePoint(true).ToScriptHash();
            var w = scriptHash.ToArray()[..25];

            var id2 = new OwnerID() { Value = ByteString.CopyFrom(w) };

            return id.ToByteString() == id2.ToByteString();
        }

        public bool ValidateContent(ObjectType t, byte[] payload)
        {
            switch (t)
            {
                case ObjectType.Regular:
                    break;
                case ObjectType.Tombstone:
                    if (payload.Length == 0)
                        return false;
                    var tombstone = Tombstone.FromByteArray(payload);
                    foreach (var address in tombstone.Addresses)
                    {
                        if (address.ObjectId is null || address.ContainerId is null)
                        {
                            return false;
                        }
                    }
                    if (deleteHandler != null)
                        deleteHandler.DeleteObjects(tombstone.Addresses.ToArray());
                    break;
                case ObjectType.StorageGroup:
                    break;
                default:
                    break;
            }
            return true;
        }

    }
}
