using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Network.RPC.Models;

namespace Neo.Network.RPC.Tests.Models
{
    [TestClass]
    public class UT_RpcPeer
    {
        [TestMethod]
        public void TestToJson()
        {
            var rpcPeer = new RpcPeer()
            {
                Address = "abc",
                Port = 800
            };
            var json = rpcPeer.ToJson();
            json["address"].AsString().Should().Be("abc");
            json["port"].AsNumber().Should().Be(800);
        }
    }
}
