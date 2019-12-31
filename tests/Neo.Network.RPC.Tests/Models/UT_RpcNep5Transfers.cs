using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.IO.Json;
using Neo.Wallets;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Network.RPC.Models.Tests
{
    [TestClass]
    public class UT_RpcNep5Transfers
    {
        private RpcNep5Transfers tranfers;

        [TestInitialize]
        public void Setup()
        {
            tranfers = new RpcNep5Transfers();
        }

        [TestMethod]
        public void TestToJson()
        {
            tranfers.UserScriptHash = UInt160.Zero;
            tranfers.Sent = new List<RpcNep5Transfer> { new RpcNep5Transfer {
                Amount = 1000,
                AssetScriptHash = UInt160.Zero,
                BlockIndex =100,
                TimestampMS = 0,
                TransferNotifyIndex =0,
                TxHash = UInt256.Zero,
                UserScriptHash =UInt160.Zero
            } };
            tranfers.Received = new List<RpcNep5Transfer> { new RpcNep5Transfer {
                Amount = 1000,
                AssetScriptHash = UInt160.Zero,
                BlockIndex =100,
                TimestampMS = 0,
                TransferNotifyIndex =0,
                TxHash = UInt256.Zero,
                UserScriptHash =UInt160.Zero
            } };
            var json = tranfers.ToJson();
            json["address"].AsString().Should().Be("NKuyBkoGdZZSLyPbJEetheRhMjeznFZszf");
            ((JArray)json["sent"]).Count.Should().Be(1);
            ((JArray)json["received"]).Single()["amount"].AsString().Should().Be("1000");
        }

        [TestMethod]
        public void TestFromJson()
        {
            JObject json = JObject.Parse(@"
            {
                ""sent"": [
                    {
                        ""timestamp"": 1554283931,
                        ""asset_hash"": ""0x1aada0032aba1ef6d1f07bbd8bec1d85f5380fb3"",
                        ""transfer_address"": ""NaEVDyZ5aivkKaH6PrmLe7e1xhJViQHQz5"",
                        ""amount"": ""100000000000"",
                        ""block_index"": 368082,
                        ""transfer_notify_index"": 0,
                        ""tx_hash"": ""0x240ab1369712ad2782b99a02a8f9fcaa41d1e96322017ae90d0449a3ba52a564""
                    }
                ],
                ""received"": [
                    {
                        ""timestamp"": 1555055087,
                        ""asset_hash"": ""0xed5620eec5759861842e8182524fdb0321e6d831"",
                        ""transfer_address"": ""NaEVDyZ5aivkKaH6PrmLe7e1xhJViQHQz5"",
                        ""amount"": ""200000000000000"",
                        ""block_index"": 406373,
                        ""transfer_notify_index"": 0,
                        ""tx_hash"": ""0x73e55f8048367f86d7da92b29ea38b739f984e86759bdeacd2244d491e60e9eb""
                    },
                    {
                        ""timestamp"": 1555651816,
                        ""asset_hash"": ""0x600c4f5200db36177e3e8a09e9f18e2fc7d12a0f"",
                        ""transfer_address"": ""NaEVDyZ5aivkKaH6PrmLe7e1xhJViQHQz5"",
                        ""amount"": ""1000000"",
                        ""block_index"": 436036,
                        ""transfer_notify_index"": 0,
                        ""tx_hash"": ""0xdf7683ece554ecfb85cf41492c5f143215dd43ef9ec61181a28f922da06aba58""
                    }
                ],
                ""address"": ""NaEVDyZ5aivkKaH6PrmLe7e1xhJViQHQz5""
            }");

            var transfers = RpcNep5Transfers.FromJson(json);
            transfers.UserScriptHash.Should().Be("NaEVDyZ5aivkKaH6PrmLe7e1xhJViQHQz5".ToScriptHash());
            transfers.Sent.Single().TxHash.Should().Be(UInt256.Parse("0x240ab1369712ad2782b99a02a8f9fcaa41d1e96322017ae90d0449a3ba52a564"));
            transfers.Received.Last().TxHash.Should().Be(UInt256.Parse("0xdf7683ece554ecfb85cf41492c5f143215dd43ef9ec61181a28f922da06aba58"));
        }
    }
}
