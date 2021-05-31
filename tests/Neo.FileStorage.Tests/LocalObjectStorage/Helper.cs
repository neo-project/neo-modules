using System;
using System.Security.Cryptography;
using Google.Protobuf;
using Neo.Cryptography;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Cryptography.Tz;
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

        public static Address RandomAddress()
        {
            return new()
            {
                ContainerId = ContainerID.FromSha256Bytes(RandomSha256()),
                ObjectId = ObjectID.FromSha256Bytes(RandomSha256()),
            };
        }

        public static FSObject RandomObject(ulong size)
        {
            Address address = RandomAddress();
            FSObject obj = new();
            obj.Header = new();
            obj.Header.ContainerId = address.ContainerId;
            obj.ObjectId = address.ObjectId;
            obj.Payload = ByteString.CopyFrom(new byte[size - (ulong)(obj.ToByteArray().Length)]);
            return obj;
        }

        public static FSObject GenerateObjectWithContainerID(ContainerID cid)
        {
            byte[] privateKey = new byte[32];
            using RandomNumberGenerator rng = RandomNumberGenerator.Create();
            rng.GetBytes(privateKey);
            ECDsa key = privateKey.LoadPrivateKey();
            return new()
            {
                Header = new()
                {
                    OwnerId = key.ToOwnerID(),
                    ContainerId = cid,
                    Version = API.Refs.Version.SDKVersion(),
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
                ObjectId = RandomObjectID(),
                Payload = ByteString.CopyFrom(new byte[] { 1, 2, 3, 4 })
            };
        }
    }
}
