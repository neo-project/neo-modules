using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.LocalObjectStorage.Shards;
using FSObject = Neo.FileStorage.API.Object.Object;


namespace Neo.FileStorage.Tests.LocalObjectStorage.Shards
{
    [TestClass]
    public class UT_Shard
    {
        [TestMethod]
        public void GetWithCache_Test()
        {
            var cid = GenerateContainerID();
            var obj = GenerateRawObjectWithCID(cid);
            var shard = GetShardWithCache();

            shard.Put(obj);

            var val = shard.Get(obj.Address);

            Assert.Equals(obj, val);
        }


        [TestMethod]
        public void GetWithoutCache_Test()
        {
            var cid = GenerateContainerID();
            var obj = GenerateRawObjectWithCID(cid);
            var shard = GetShardWithoutCache();

            shard.Put(obj);

            var val = shard.Get(obj.Address);
            Assert.Equals(obj, val);
        }


        [TestMethod]
        public void GetHeadWithCache_Test()
        {
            var cid = GenerateContainerID();
            var obj = GenerateRawObjectWithCID(cid);
            var shard = GetShardWithCache();

            shard.Put(obj);

            var val = shard.Head(obj.Address, false);

            Assert.Equals(obj.Header, val);
        }


        [TestMethod]
        public void GetHeadWithoutCache_Test()
        {
            var cid = GenerateContainerID();
            var obj = GenerateRawObjectWithCID(cid);
            var shard = GetShardWithoutCache();

            shard.Put(obj);

            var val = shard.Head(obj.Address, false);
            Assert.Equals(obj.Header, val);
        }

        [TestMethod]
        public void DeleteWithCache_Test()
        {
            var cid = GenerateContainerID();
            var obj = GenerateRawObjectWithCID(cid);
            var shard = GetShardWithCache();

            shard.Put(obj);

            var val = shard.Get(obj.Address);

            shard.Delete(obj.Address);

            var valNull = shard.Get(obj.Address);
            Assert.IsNull(valNull);
        }


        [TestMethod]
        public void DeleteWithoutCache_Test()
        {
            var cid = GenerateContainerID();
            var obj = GenerateRawObjectWithCID(cid);
            var shard = GetShardWithoutCache();

            shard.Put(obj);

            var val = shard.Get(obj.Address);

            shard.Delete(obj.Address);

            var valNull = shard.Get(obj.Address);
            Assert.IsNull(valNull);
        }



        [TestMethod]
        public void InhumeWithCache_Test()
        {
            var cid = GenerateContainerID();
            var obj = GenerateRawObjectWithCID(cid);
            var shard = GetShardWithCache();

            shard.Put(obj);

            var val = shard.Get(obj.Address);

            shard.Inhume(obj.Address, obj.Address);

            var valNull = shard.Get(obj.Address);
            Assert.IsNull(valNull);
        }


        [TestMethod]
        public void InhumeWithoutCache_Test()
        {
            var cid = GenerateContainerID();
            var obj = GenerateRawObjectWithCID(cid);
            var shard = GetShardWithoutCache();

            shard.Put(obj);

            var val = shard.Get(obj.Address);

            shard.Inhume(obj.Address, obj.Address);

            var valNull = shard.Get(obj.Address);
            Assert.IsNull(valNull);
        }



        [TestMethod]
        public void ListWithCache_Test()
        {
            const int C = 5;
            const int N = 10;
            var addresses = new HashSet<Address>();
            var shard = GetShardWithCache();

            for (int i = 0; i < C; i++)
            {
                var cid = GenerateContainerID();
                for (int j = 0; j < N; j++)
                {
                    var obj = GenerateRawObjectWithCID(cid);
                    shard.Put(obj);
                    addresses.Add(obj.Address);
                }
            }

            var list = shard.List();

            Assert.AreEqual(addresses.Count, list.Count()); ;
            Assert.AreEqual(0, list.Except(addresses).Count());
        }


        [TestMethod]
        public void ListWithoutCache_Test()
        {
            const int C = 5;
            const int N = 10;
            var addresses = new HashSet<Address>();
            var shard = GetShardWithoutCache();

            for (int i = 0; i < C; i++)
            {
                var cid = GenerateContainerID();
                for (int j = 0; j < N; j++)
                {
                    var obj = GenerateRawObjectWithCID(cid);
                    shard.Put(obj);
                    addresses.Add(obj.Address);
                }
            }

            var list = shard.List();

            Assert.AreEqual(addresses.Count, list.Count()); ;
            Assert.AreEqual(0, list.Except(addresses).Count());

        }

        private ContainerID GenerateContainerID()
        {
            var bytes = new byte[32];
            new Random().NextBytes(bytes);
            return ContainerID.FromSha256Bytes(bytes);
        }

        private ObjectID GenerateObjectID()
        {
            var bytes = new byte[32];
            new Random().NextBytes(bytes);
            return ObjectID.FromSha256Bytes(bytes);
        }


        private OwnerID GenerateOwnerID()
        {
            var bytes = new byte[25];
            new Random().NextBytes(bytes);
            return OwnerID.FromByteArray(bytes);
        }
        private FSObject GenerateRawObjectWithCID(ContainerID cid)
        {
            var obj = new FSObject();
            obj.ObjectId = GenerateObjectID();
            obj.Header = new Header();
            obj.Header.ContainerId = cid;
            obj.Header.OwnerId = GenerateOwnerID();
            return obj;
        }


        private Shard GetShardWithCache()
        {
            return new Shard(true);
        }

        private Shard GetShardWithoutCache()
        {
            return new Shard(false);
        }
    }
}
