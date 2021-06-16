using System;
using System.Security.Cryptography;
using Google.Protobuf;
using Neo.Cryptography;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Cryptography.Tz;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Tests.LocalObjectStorage
{
    public static class Helper
    {
        public static byte[] RandomSha256()
        {
            byte[] result = new byte[32];
            var rand = new Random();
            rand.NextBytes(result);
            return result;
        }

        public static ContainerID RandomContainerID()
        {
            return ContainerID.FromSha256Bytes(RandomSha256());
        }

        public static ObjectID RandomObjectID()
        {
            return ObjectID.FromSha256Bytes(RandomSha256());
        }

        public static Address RandomAddress(ContainerID cid = null)
        {
            return new()
            {
                ContainerId = cid ?? RandomContainerID(),
                ObjectId = RandomObjectID(),
            };
        }

        public static FSObject RandomObject(int size = 1024)
        {
            return RandomObject(RandomContainerID(), size);
        }

        public static FSObject RandomObject(ContainerID cid, int size = 1024)
        {
            byte[] privateKey = new byte[32];
            byte[] payload = new byte[size];
            byte[] signature = new byte[64];
            using RandomNumberGenerator rng = RandomNumberGenerator.Create();
            rng.GetBytes(privateKey);
            rng.GetBytes(payload);
            rng.GetBytes(signature);
            ECDsa key = privateKey.LoadPrivateKey();
            return new()
            {
                Header = new()
                {
                    OwnerId = key.ToOwnerID(),
                    ContainerId = cid,
                    Version = API.Refs.Version.SDKVersion(),
                    ObjectType = ObjectType.Regular,
                    PayloadLength = (ulong)size,
                    PayloadHash = new()
                    {
                        Type = ChecksumType.Sha256,
                        Sum = ByteString.CopyFrom(privateKey.Sha256())
                    },
                    HomomorphicHash = new()
                    {
                        Type = ChecksumType.Tz,
                        Sum = ByteString.CopyFrom(new TzHash().ComputeHash(privateKey))
                    }
                },
                Signature = new()
                {
                    Key = ByteString.CopyFrom(key.PublicKey()),
                    Sign = ByteString.CopyFrom(signature)
                },
                ObjectId = RandomObjectID(),
                Payload = ByteString.CopyFrom(payload)
            };
        }
    }
}
