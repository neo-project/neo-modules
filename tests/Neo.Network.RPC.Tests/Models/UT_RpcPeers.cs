using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.IO.Json;
using System.Linq;

namespace Neo.Network.RPC.Models.Tests
{
    [TestClass]
    public class UT_RpcPeers
    {
        [TestMethod]
        public void TestToJson()
        {
            var rpcPeers = new RpcPeers()
            {
                Unconnected = new RpcPeer[] {
                    new RpcPeer()
                    {
                        Address = "Unconnected",
                        Port = 600
                    }
                },
                Bad = new RpcPeer[] {
                    new RpcPeer()
                    {
                        Address = "Bad",
                        Port = 700
                    }
                },
                Connected = new RpcPeer[] {
                    new RpcPeer()
                    {
                        Address = "Connected",
                        Port = 800
                    }
                }
            };
            var json = rpcPeers.ToJson();
            ((JArray)json["unconnected"]).Count.Should().Be(1);
            ((JArray)json["unconnected"]).Single()["address"].AsString().Should().Be("Unconnected");
            ((JArray)json["unconnected"]).Single()["port"].AsNumber().Should().Be(600);
            ((JArray)json["bad"]).Count.Should().Be(1);
            ((JArray)json["connected"]).Count.Should().Be(1);

            var copy = RpcPeers.FromJson(json);
            copy.ToJson().ToString().Should().Be(json.ToString());
        }
    }
}
