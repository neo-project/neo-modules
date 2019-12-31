using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.IO.Json;
using Neo.SmartContract;
using Neo.VM;
using Neo.VM.Types;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Neo.Network.RPC.Models.Tests
{
    [TestClass()]
    public class UT_RpcApplicationLog
    {
        [TestMethod()]
        public void ToJsonTest()
        {
            StackItem item = new Array(new StackItem[] { "7472616e73666572".HexToBytes(), "d086ac0ed3e578a1afd3c0a2c0d8f0a180405be2".HexToBytes() });

            var rpcAppLog = new RpcApplicationLog
            {
                TxId = UInt256.Zero,
                Trigger = TriggerType.Application,
                VMState = VM.VMState.HALT,
                GasConsumed = 10000,
                Stack = new List<StackItem>() { 1 },
                Notifications = new List<NotifyEventArgs>
                {
                   new NotifyEventArgs(null, UInt160.Parse("0x78e6d16b914fe15bc16150aeb11d0c2a8e532bdd"), item)
                }
            };
            var json = rpcAppLog.ToJson();
            json["txid"].AsString().Should().Be("0x0000000000000000000000000000000000000000000000000000000000000000");
            json["trigger"].AsString().Should().Be("Application");
            json["vmstate"].AsString().Should().Be("HALT");
            json["gas_consumed"].AsString().Should().Be("10000");
            ((JArray)json["stack"]).Single()["value"].AsString().Should().Be("1");
            ((JArray)json["notifications"]).Single()["contract"].AsString().Should().Be("0x78e6d16b914fe15bc16150aeb11d0c2a8e532bdd");
            ((JArray)json["notifications"]).Single()["state"]["type"].AsString().ToString().Should().Be("Array");
        }

        [TestMethod()]
        public void FromJsonTest()
        {
            JObject json = JObject.Parse(@"
            {
                ""txid"": ""0x92b1ecc0e8ca8d6b03db7fe6297ed38aa5578b3e6316c0526b414b453c89e20d"",
                ""trigger"": ""Application"",
                ""vmstate"": ""HALT"",
                ""gas_consumed"": ""291200000"",
                ""stack"": [
                    { 
                        ""type"": ""Integer"",
                        ""value"": ""1"" 
                    } 
                ],
                ""notifications"": [
                    {
                        ""contract"": ""0x78e6d16b914fe15bc16150aeb11d0c2a8e532bdd"",
                        ""state"": {
                            ""type"": ""Array"",
                            ""value"": [
                                {
                                    ""type"": ""ByteArray"",
                                    ""value"": ""7472616e73666572""
                                },
                                {
                                    ""type"": ""ByteArray"",
                                    ""value"": ""d086ac0ed3e578a1afd3c0a2c0d8f0a180405be2""
                                }
                            ]
                        }
                    }
                ]
            }");

            RpcApplicationLog log = RpcApplicationLog.FromJson(json);
            log.TxId.Should().Be(UInt256.Parse("0x92b1ecc0e8ca8d6b03db7fe6297ed38aa5578b3e6316c0526b414b453c89e20d"));
            log.Stack.Single().ToParameter().Value.Should().Be(new BigInteger(1));
            log.Notifications.Single().ScriptHash.Should().Be(UInt160.Parse("0x78e6d16b914fe15bc16150aeb11d0c2a8e532bdd"));
            log.Notifications.Single().State.ToParameter().Type.Should().Be(ContractParameterType.Array);
        }
    }
}
