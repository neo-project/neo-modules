using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Google.Protobuf;
using Neo.FileStorage.API.Acl;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Cryptography.Tz;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.Session;
using FSAnnouncement = Neo.FileStorage.API.Container.AnnounceUsedSpaceRequest.Types.Body.Types.Announcement;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Storage.Tests
{
    public static class Helper
    {
        public static byte[] RandomPrivatekey()
        {
            var privateKey = new byte[32];
            using RandomNumberGenerator rng = RandomNumberGenerator.Create();
            rng.GetBytes(privateKey);
            return privateKey;
        }

        public static byte[] RandomSignature()
        {
            byte[] signature = new byte[64];
            using RandomNumberGenerator rng = RandomNumberGenerator.Create();
            rng.GetBytes(signature);
            return signature;
        }

        public static OwnerID RandomOwnerID()
        {
            var key = RandomPrivatekey().LoadPrivateKey();
            return OwnerID.FromPublicKey(key.PublicKey());
        }

        public static SessionToken.Types.Body.Types.TokenLifetime RandomSessionTokenLifeTime()
        {
            return new()
            {
                Exp = 1,
                Iat = 2,
                Nbf = 3,
            };
        }

        public static BearerToken.Types.Body.Types.TokenLifetime RandomBearerTokenLifeTime()
        {
            return new()
            {
                Exp = 1,
                Iat = 2,
                Nbf = 3,
            };
        }

        public static byte[] RandomSha256()
        {
            byte[] result = new byte[32];
            var rand = new Random();
            rand.NextBytes(result);
            return result;
        }

        public static ContainerID RandomContainerID()
        {
            return ContainerID.FromValue(RandomSha256());
        }

        public static ObjectID RandomObjectID()
        {
            return ObjectID.FromValue(RandomSha256());
        }

        public static List<ObjectID> RandomObjectIDs(int n)
        {
            List<ObjectID> results = new();
            for (int i = 0; i < n; i++)
                results.Add(RandomObjectID());
            return results;
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
            byte[] privateKey = RandomPrivatekey();
            byte[] payload = new byte[size];
            byte[] signature = new byte[64];
            var random = new Random();
            random.NextBytes(payload);
            random.NextBytes(signature);
            ECDsa key = privateKey.LoadPrivateKey();
            FSObject obj = new()
            {
                Header = new()
                {
                    OwnerId = OwnerID.FromPublicKey(key.PublicKey()),
                    ContainerId = cid,
                    Version = API.Refs.Version.SDKVersion(),
                    ObjectType = ObjectType.Regular,
                    PayloadLength = (ulong)size,
                    PayloadHash = new()
                    {
                        Type = ChecksumType.Sha256,
                        Sum = ByteString.CopyFrom(payload.Sha256())
                    },
                    HomomorphicHash = new()
                    {
                        Type = ChecksumType.Tz,
                        Sum = ByteString.CopyFrom(new TzHash().ComputeHash(payload))
                    }
                },
                Payload = ByteString.CopyFrom(payload)
            };
            obj.SetVerificationFields(key);
            return obj;
        }

        public static ulong RandomUInt64(ulong max = ulong.MaxValue)
        {
            var random = new Random();
            var buffer = new byte[sizeof(ulong)];
            random.NextBytes(buffer);
            return BitConverter.ToUInt64(buffer) % max;
        }

        public static FSAnnouncement RandomAnnouncement()
        {
            return new()
            {
                Epoch = RandomUInt64(),
                ContainerId = RandomContainerID(),
                UsedSpace = RandomUInt64(1 << 40),
            };
        }

        public static SessionToken RandomSessionToken()
        {
            return new()
            {
                Body = new()
                {
                    Id = ByteString.CopyFrom(new byte[] { 1 }),
                    SessionKey = ByteString.CopyFrom(new byte[] { 2 }),
                    OwnerId = RandomOwnerID(),
                    Lifetime = RandomSessionTokenLifeTime(),
                    Object = new()
                    {
                        Verb = ObjectSessionContext.Types.Verb.Head,
                        Address = RandomAddress(),
                    }
                }
            };
        }

        public static BearerToken RandomBearerToken()
        {
            var key = RandomPrivatekey().LoadPrivateKey();
            return new()
            {
                Signature = new()
                {
                    Key = ByteString.CopyFrom(key.PublicKey()),
                    Sign = ByteString.CopyFrom(RandomSignature()),
                },
                Body = new()
                {
                    OwnerId = OwnerID.FromPublicKey(key.PublicKey()),
                    EaclTable = new()
                    {
                        ContainerId = RandomContainerID(),
                        Version = Neo.FileStorage.API.Refs.Version.SDKVersion(),
                    },
                    Lifetime = RandomBearerTokenLifeTime(),
                }
            };
        }

        public static void TestNodeMatrix(int[] dim, out List<List<Node>> nss, out List<List<string>> addrss)
        {
            nss = new();
            addrss = new();
            int sum = 0;
            foreach (var i in dim)
            {
                List<Node> list = new();
                List<string> addrs = new();
                for (int j = 0; j < i; j++)
                {
                    var ni = new NodeInfo();
                    string addr = $"/ip4/192.168.0.{i}/tcp/{60000 + sum + j}";
                    ni.Addresses.Add(addr);
                    list.Add(new Node
                    (sum + j, ni));
                    addrs.Add(addr);
                }
                sum += i;
                nss.Add(list);
                addrss.Add(addrs);
            }
        }
    }
}
