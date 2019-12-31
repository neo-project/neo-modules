using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Neo.Network.RPC.Models.Tests
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
