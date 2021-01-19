using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo;
using NeoFS.API.v2.Container;
using NeoFS.API.v2.Refs;
using System;
using System.Collections.Generic;

namespace Neo.Plugins.FSNode.Tests.Services.Object.Delete
{
    [TestClass]
    public class UT_Util
    {
        [TestMethod]
        public void TestToByteArray()
        {
            var list = new List<ContainerID>();
            var cid = new ContainerID
            {
                Value = ByteString.CopyFrom(new byte[] { 0xff }),
            };

            var body = new ListResponse.Types.Body();
            body.ContainerIds.Add(cid);

            list.Add(cid);

            Assert.AreEqual(body.ToByteArray().ToHexString(), list.ToRepeatedField().ToByteArray().ToHexString());
        }
    }
}
