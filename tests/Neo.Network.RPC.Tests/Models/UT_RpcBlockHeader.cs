using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Network.RPC.Tests;

namespace Neo.Network.RPC.Models.Tests
{
    [TestClass]
    public class UT_RpcBlockHeader
    {
        [TestMethod]
        public void TestToJson()
        {
            var rpcBlockHeader = new RpcBlockHeader
            {
                Header = TestUtils.GetHeader(),
                Confirmations = 1,
                NextBlockHash = UInt256.Zero
            };
            var json = rpcBlockHeader.ToJson();
            json["previousblockhash"].AsString().Should().Be("0x0000000000000000000000000000000000000000000000000000000000000000");
            json["confirmations"].AsNumber().Should().Be(1);
        }
    }
}
