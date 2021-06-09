using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.API.Object;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.LocalObjectStorage.Engine;
using FSObject = Neo.FileStorage.API.Object.Object;

namespace Neo.FileStorage.Tests.LocalObjectStorage.Engine
{
    [TestClass]
    public class UT_Engine
    {
        [TestMethod]
        public void TestInhume()
        {
            ContainerID cid = Helper.RandomContainerID();
            SplitID sid = new();
            SearchFilters filters = new();
            filters.AddRootFilter();
            Address tombstone = Helper.RandomObject(cid).Address;
            FSObject parent = Helper.RandomObject(cid);
            FSObject child = Helper.RandomObject(cid);
            child.Parent = parent;
            child.SplitId = sid;
            FSObject link = Helper.RandomObject(cid);
            link.Parent = parent;
            link.Children = new ObjectID[] { child.ObjectId };
            link.SplitId = sid;
            string path = "./engine_test";
            try
            {
                using StorageEngine engine = new();
                engine.AddShard(path, false);
                engine.Put(parent);
                engine.Inhume(parent.Address);
            }
            finally
            {
                Directory.Delete(path, true);
            }
        }
    }
}
