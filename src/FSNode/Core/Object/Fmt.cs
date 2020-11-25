using Google.Protobuf;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.SmartContract;
using NeoFS.API.v2.Object;
using NeoFS.API.v2.Refs;
using System;
using V2Object = NeoFS.API.v2.Object.Object;

namespace Neo.Fs.Core.Object
{
    public interface IDeleteHandler
    {
        void DeleteObjects(params Address[] objAddrs);
    }

    public class Cfg
    {
        private IDeleteHandler deleteHandler;
    }

    public class FormatValidator
    {
        private Cfg cfg;

        public FormatValidator(FormatValidatorOption[] opts)
        {
            var c = new Cfg();

            foreach (var opt in opts)
            {
                opt(c);
            }

            this.cfg = c;
        }

        public void Validate(V2Object obj)
        {
            if (obj is null)
                throw new ArgumentNullException("object is null");
            else if (obj.ObjectId is null)
                throw new ArgumentException("missing identifier");
            else if (obj.Header is null || obj.Header.ContainerId is null)
                throw new ArgumentException("missing container identifier");

            while (obj != null)
            {



            }
        }

        private void ValidateSignatureKey(V2Object obj)
        {
            var token = obj.Header.SessionToken;
            var key = obj.Signature.Key;

            if (token is null || !token.Body.SessionKey.Equals(key))
                CheckOwnerKey(obj.Header.OwnerId, obj.Signature.Key.ToByteArray());

            // TODO: perform token verification
        }

        private void CheckOwnerKey(OwnerID id, byte[] key)
        {
            var pubKey = ECPoint.FromBytes(key, ECCurve.Secp256r1);
            var scriptHash = pubKey.EncodePoint(true).ToScriptHash();
            var w = scriptHash.ToArray()[..25];

            var id2 = new OwnerID() { Value = ByteString.CopyFrom(w) };

            if (id.ToByteString() != id2.ToByteString())
                throw new Exception(string.Format("different owner identifiers: {0}, {1}", id.ToByteString(), id2.ToByteString()));
        }

        public void ValidateContent(ObjectType t, byte[] payload)
        {
            switch (t)
            {
                case ObjectType.Regular:
                    break;
                case ObjectType.Tombstone:
                    if (payload.Length == 0)
                        throw new Exception("empty payload in tombstone");
                    break;
                case ObjectType.StorageGroup:
                    break;
                default:
                    break;
            }

        }

    }

    public delegate void FormatValidatorOption(Cfg cfg);


}
