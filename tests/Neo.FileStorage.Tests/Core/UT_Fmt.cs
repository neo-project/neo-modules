using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.API.Netmap;
using System;
using H = Neo.FileStorage.Core.Container.Helper;
using V2Container = Neo.FileStorage.API.Container.Container;
using V2Version = Neo.FileStorage.API.Refs.Version;
using V2OwnerID = Neo.FileStorage.API.Refs.OwnerID;
using Google.Protobuf;

namespace Neo.Plugins.FSNode.Tests
{
    [TestClass]
    public class UT_Fmt
    {
        [TestMethod]
        public void TestCheckFormat()
        {
            var c = new V2Container();
            Assert.ThrowsException<ArgumentException>(() => H.CheckFormat(c));

            var policy = new PlacementPolicy();
            c.PlacementPolicy = policy;
            Assert.ThrowsException<ArgumentException>(() => H.CheckFormat(c));

            c.Version = V2Version.SDKVersion();
            Assert.ThrowsException<ArgumentException>(() => H.CheckFormat(c));

            c.OwnerId = V2OwnerID.Frombytes(new byte[25]);
            Assert.ThrowsException<ArgumentException>(() => H.CheckFormat(c));

            c.Nonce = ByteString.CopyFrom(Guid.NewGuid().ToByteArray());
            Assert.ThrowsException<AssertFailedException>(
                () => Assert.ThrowsException<ArgumentException>(
                    () => H.CheckFormat(c)
                    )
                );

        }
    }
}
