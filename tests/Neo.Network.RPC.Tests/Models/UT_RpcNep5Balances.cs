using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.IO.Json;
using System.Linq;
using System.Numerics;

namespace Neo.Network.RPC.Models.Tests
{
    [TestClass]
    public class UT_RpcNep5Balances
    {
        private RpcNep5Balances balances;

        [TestInitialize]
        public void Setup()
        {
            balances = new RpcNep5Balances()
            {
                UserScriptHash = UInt160.Zero,
                Balances = new RpcNep5Balance[] {
                    new RpcNep5Balance()
                    {
                        AssetHash = UInt160.Zero,
                        Amount = BigInteger.Zero,
                        LastUpdatedBlock = 0
                    },
                    new RpcNep5Balance()
                    {
                        AssetHash = UInt160.Parse("0xa400ff00ff00ff00ff00ff00ff00ff00ff00ff01"),
                        Amount = new BigInteger(1),
                        LastUpdatedBlock = 1
                    }
                }.ToList()
            };
        }

        [TestMethod]
        public void TestAddress()
        {
            balances.UserScriptHash.Should().Be(UInt160.Zero);
        }

        [TestMethod]
        public void TestBalances()
        {
            balances.Balances.Count.Should().Be(2);
        }

        [TestMethod]
        public void TestToJson()
        {
            var json = balances.ToJson();
            json["address"].AsString().Should().Be("NKuyBkoGdZZSLyPbJEetheRhMjeznFZszf");
            ((JArray)json["balance"]).Count.Should().Be(2);
        }

        [TestMethod]
        public void TestFromJson()
        {
            var json = balances.ToJson();
            var rpcNep5Balances = RpcNep5Balances.FromJson(json);
            rpcNep5Balances.UserScriptHash.Should().Be(UInt160.Zero);
            rpcNep5Balances.Balances.Count.Should().Be(2);
        }
    }
}
