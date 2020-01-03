using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Network.RPC.Tests;

namespace Neo.Network.RPC.Models.Tests
{
    [TestClass]
    public class UT_RpcBlock
    {
        [TestMethod]
        public void TestToJson()
        {
            var rpcBlock = new RpcBlock
            {
                Block = TestUtils.GetBlock(1),
                Confirmations = 1,
                NextBlockHash = UInt256.Zero
            };
            var json = rpcBlock.ToJson();
            json["previousblockhash"].AsString().Should().Be("0x0000000000000000000000000000000000000000000000000000000000000000");
            json["confirmations"].AsNumber().Should().Be(1);

            var copy = RpcBlock.FromJson(json);
            copy.ToJson().ToString().Should().Be(json.ToString());
        }
    }
}
