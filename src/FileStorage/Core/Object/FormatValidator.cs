using Google.Protobuf;
using Neo.Cryptography.ECC;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Core.Netmap;
using Neo.IO;
using Neo.SmartContract;
using V2Object = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Core.Object
{
    public class FormatValidator
    {
        private IObjectDeleteHandler deleteHandler;
        private INetState netState;

        public FormatValidator(IObjectDeleteHandler handler, INetState state)
        {
            deleteHandler = handler;
            netState = state;
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
                obj = obj.Parent;
                if (!ValidateSignatureKey(obj)) return false;
                if (!obj.VerifyIDSignature()) return false;
                if (!obj.VerifyID()) return false;
            }
            return true;
        }

        private bool ValidateSignatureKey(V2Object obj)
        {
            var token = obj.SessionToken;
            var key = obj.Signature.Key;

            if (token is null || !token.Body.SessionKey.Equals(key))
                return obj.OwnerId == key.ToByteArray().PublicKeyToOwnerID();

            // TODO: perform token verification
            return true;
        }

        public bool CheckExpiration(V2Object obj)
        {
            return true;
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
