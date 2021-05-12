using Google.Protobuf;
using Neo.FileStorage.API.Refs;
using System;
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
    }
}
