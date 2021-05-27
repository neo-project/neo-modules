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
            Address tombstone = Helper.GenerateObjectWithContainerID(cid).Address;
            FSObject parent = Helper.GenerateObjectWithContainerID(cid);
            FSObject child = Helper.GenerateObjectWithContainerID(cid);
            child.Parent = parent;
            child.SplitId = sid;
            FSObject link = Helper.GenerateObjectWithContainerID(cid);
            link.Parent = parent;
            link.Children = new ObjectID[] { child.ObjectId };
            link.SplitId = sid;
            string path = "./engine_test";
            using StorageEngine engine = new();
            engine.AddShard(path, false);
            engine.Put(parent);
            engine.Inhume(parent.Address);
        }
    }
}
