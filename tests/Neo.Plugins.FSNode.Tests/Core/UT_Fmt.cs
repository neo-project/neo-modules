using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoFS.API.v2.Netmap;
using System;
using H = Neo.FSNode.Core.Container.Helper;
using V2Container = NeoFS.API.v2.Container.Container;
using V2Version = NeoFS.API.v2.Refs.Version;
using V2OwnerID = NeoFS.API.v2.Refs.OwnerID;
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

            c.OwnerId = V2OwnerID.FromByteArray(new byte[25]);
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
