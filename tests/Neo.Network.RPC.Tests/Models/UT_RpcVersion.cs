using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Neo.Network.RPC.Models.Tests
{
    [TestClass]
    public class UT_RpcVersion
    {
        [TestMethod]
        public void TestToJson()
        {
            var version = new RpcVersion()
            {
                TcpPort = 800,
                WsPort = 900,
                Nonce = 1,
                UserAgent = "agent"
            };
            var json = version.ToJson();
            json["tcpPort"].AsNumber().Should().Be(800);
            json["wsPort"].AsNumber().Should().Be(900);
            json["nonce"].AsNumber().Should().Be(1);
            json["useragent"].AsString().Should().Be("agent");

            var copy = RpcVersion.FromJson(json);
            copy.ToJson().ToString().Should().Be(json.ToString());
        }
    }
}
