using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Network.RPC.Models;

namespace Neo.Network.RPC.Tests.Models
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
        }
    }
}
