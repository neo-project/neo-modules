using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.IO.Json;

namespace Neo.Network.RPC.Models.Tests
{
    [TestClass]
    public class UT_RpcRequest
    {
        [TestMethod]
        public void TestFromJson()
        {
            var req = new RpcRequest()
            {
                Id = 1,
                Jsonrpc = "myrpc",
                Method = "get",
                Params = new JObject[] {
                    new JBoolean(true)
                }
            };
            var json = req.ToJson();
            var rpcRequest = RpcRequest.FromJson(json);
            rpcRequest.Jsonrpc.Should().Be("myrpc");
            rpcRequest.Method.Should().Be("get");
            rpcRequest.Id.Should().Be(1);
            rpcRequest.Params.Length.Should().Be(1);

            var copy = RpcRequest.FromJson(json);
            copy.ToJson().ToString().Should().Be(json.ToString());
        }
    }
}
