using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.LocalObjectStorage.Shards;
using static Neo.FileStorage.Tests.LocalObjectStorage.Helper;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Tests.LocalObjectStorage.Shards
{
    [TestClass]
    public class UT_Shard
    {
        private Shard NewShard(string testName, bool useCache)
        {
            BlobStorageSettings blobStorSettings = BlobStorageSettings.Default;
            blobStorSettings.Path = testName + "/" + blobStorSettings.Path;
            return new(useCache)
            {
                ID = new(),
                BlobStorage = new(blobStorSettings),
                Metabase = new(testName + "/metabase"),
                WorkPool = null
            };
        }

        [TestMethod]
        public void TestShardGetWithoutCache()
        {
            try
            {
                using var shard = NewShard(nameof(TestShardGetWithoutCache), false);
                // small object
                var obj = RandomObject();
                obj.Header.Attributes.Add(new Header.Types.Attribute { Key = "foo", Value = "bar" });
                shard.Put(obj);
                var o = shard.Get(obj.Address);
                Assert.IsNotNull(o);
            }
            finally
            {
                Directory.Delete(nameof(TestShardGetWithoutCache), true);
            }
        }
    }
}
