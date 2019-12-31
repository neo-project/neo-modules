using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using Neo.IO;
using Neo.IO.Json;
using Neo.Ledger;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC.Models;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.VM;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Neo.Network.RPC.Tests
{
    [TestClass]
    public class UT_RpcClient
    {
        RpcClient rpc;
        Mock<HttpMessageHandler> handlerMock;

        [TestInitialize]
        public void TestSetup()
        {
            handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            // use real http client with mocked handler here
            var httpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri("http://seed1.neo.org:10331"),
            };

            rpc = new RpcClient(httpClient);
        }

        private void MockResponse(string content)
        {
            handlerMock.Protected()
               // Setup the PROTECTED method to mock
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.IsAny<HttpRequestMessage>(),
                  ItExpr.IsAny<CancellationToken>()
               )
               // prepare the expected response of the mocked http call
               .ReturnsAsync(new HttpResponseMessage()
               {
                   StatusCode = HttpStatusCode.OK,
                   Content = new StringContent(content),
               })
               .Verifiable();
        }

        private JObject CreateErrorResponse(JObject id, int code, string message, JObject data = null)
        {
            JObject response = CreateResponse(id);
            response["error"] = new JObject();
            response["error"]["code"] = code;
            response["error"]["message"] = message;
            if (data != null)
                response["error"]["data"] = data;
            return response;
        }

        private JObject CreateResponse(JObject id)
        {
            JObject response = new JObject();
            response["jsonrpc"] = "2.0";
            response["id"] = id;
            return response;
        }

        [TestMethod]
        public void TestErrorResponse()
        {
            JObject response = CreateErrorResponse(null, -32700, "Parse error");
            MockResponse(response.ToString());
            try
            {
                var result = rpc.GetBlockHex("773dd2dae4a9c9275290f89b56e67d7363ea4826dfd4fc13cc01cf73a44b0d0e");
            }
            catch (RpcException ex)
            {
                Assert.AreEqual(-32700, ex.HResult);
                Assert.AreEqual("Parse error", ex.Message);
            }
        }

        [TestMethod]
        public void TestConstructorByUrlAndDispose()
        {
            //dummy url for test
            var client = new RpcClient("http://www.xxx.yyy");
            Action action = () => client.Dispose();
            action.Should().NotThrow<Exception>();
        }

        [TestMethod]
        public void TestConstructorWithBasicAuth()
        {
            var client = new RpcClient("http://www.xxx.yyy", "krain", "123456");
            client.Dispose();
        }

        [TestMethod]
        public void TestGetBestBlockHash()
        {
            JObject response = CreateResponse(1);
            response["result"] = "000000002deadfa82cbc4682f5800";
            MockResponse(response.ToString());

            var result = rpc.GetBestBlockHash();
            Assert.AreEqual("000000002deadfa82cbc4682f5800", result);
        }

        [TestMethod]
        public void TestGetBlockHex()
        {
            JObject response = CreateResponse(1);
            response["result"] = "000000002deadfa82cbc4682f5800";
            MockResponse(response.ToString());

            var result = rpc.GetBlockHex("773dd2dae4a9c9275290f89b56e67d7363ea4826dfd4fc13cc01cf73a44b0d0e");
            Assert.AreEqual("000000002deadfa82cbc4682f5800", result);
        }

        [TestMethod]
        public void TestGetBlock()
        {
            // create block
            var block = TestUtils.GetBlock(3);

            JObject json = block.ToJson();
            json["confirmations"] = 20;
            JObject response = CreateResponse(1);
            response["result"] = json;
            MockResponse(response.ToString());

            var result = rpc.GetBlock("773dd2dae4a9c9275290f89b56e67d7363ea4826dfd4fc13cc01cf73a44b0d0e");
            Assert.AreEqual(block.Hash.ToString(), result.Block.Hash.ToString());
            Assert.IsNull(result.NextBlockHash);
            Assert.AreEqual(20, result.Confirmations);
            Assert.AreEqual(block.Transactions.Length, result.Block.Transactions.Length);
            Assert.AreEqual(block.Transactions[0].Hash.ToString(), result.Block.Transactions[0].Hash.ToString());

            // verbose with confirmations
            json["confirmations"] = 20;
            json["nextblockhash"] = "773dd2dae4a9c9275290f89b56e67d7363ea4826dfd4fc13cc01cf73a44b0d0e";
            MockResponse(response.ToString());
            result = rpc.GetBlock("773dd2dae4a9c9275290f89b56e67d7363ea4826dfd4fc13cc01cf73a44b0d0e");
            Assert.AreEqual(block.Hash.ToString(), result.Block.Hash.ToString());
            Assert.AreEqual(20, result.Confirmations);
            Assert.AreEqual("0x773dd2dae4a9c9275290f89b56e67d7363ea4826dfd4fc13cc01cf73a44b0d0e", result.NextBlockHash.ToString());
            Assert.AreEqual(block.Transactions.Length, result.Block.Transactions.Length);
            Assert.AreEqual(block.Transactions[0].Hash.ToString(), result.Block.Transactions[0].Hash.ToString());
        }

        [TestMethod]
        public void TestGetBlockCount()
        {
            JObject response = CreateResponse(1);
            response["result"] = 100;
            MockResponse(response.ToString());

            var result = rpc.GetBlockCount();
            Assert.AreEqual(100u, result);
        }

        [TestMethod]
        public void TestGetBlockHash()
        {
            JObject response = CreateResponse(1);
            response["result"] = "0x4c1e879872344349067c3b1a30781eeb4f9040d3795db7922f513f6f9660b9b2";
            MockResponse(response.ToString());

            var result = rpc.GetBlockHash(100);
            Assert.AreEqual("0x4c1e879872344349067c3b1a30781eeb4f9040d3795db7922f513f6f9660b9b2", result);
        }

        [TestMethod]
        public void TestGetBlockHeaderHex()
        {
            JObject response = CreateResponse(1);
            response["result"] = "0x4c1e879872344349067c3b1a30781eeb4f9040d3795db7922f513f6f9660b9b2";
            MockResponse(response.ToString());

            var result = rpc.GetBlockHeaderHex("100");
            Assert.AreEqual("0x4c1e879872344349067c3b1a30781eeb4f9040d3795db7922f513f6f9660b9b2", result);
        }

        [TestMethod]
        public void TestGetBlockHeader()
        {
            Header header = TestUtils.GetHeader();

            JObject json = header.ToJson();
            json["confirmations"] = 20;
            JObject response = CreateResponse(1);
            response["result"] = json;
            MockResponse(response.ToString());

            var result = rpc.GetBlockHeader("100");
            Assert.AreEqual(header.Hash.ToString(), result.Header.Hash.ToString());
            Assert.IsNull(result.NextBlockHash);
            Assert.AreEqual(20, result.Confirmations);

            json["confirmations"] = 20;
            json["nextblockhash"] = "4c1e879872344349067c3b1a30781eeb4f9040d3795db7922f513f6f9660b9b2";
            MockResponse(response.ToString());
            result = rpc.GetBlockHeader("100");
            Assert.AreEqual(header.Hash.ToString(), result.Header.Hash.ToString());
            Assert.AreEqual(20, result.Confirmations);
        }

        [TestMethod]
        public void TestGetBlockSysFee()
        {
            JObject response = CreateResponse(1);
            response["result"] = "195500";
            MockResponse(response.ToString());

            var result = rpc.GetBlockSysFee(100);
            Assert.AreEqual("195500", result);
        }

        [TestMethod]
        public void TestGetConnectionCount()
        {
            JObject response = CreateResponse(1);
            response["result"] = 9;
            MockResponse(response.ToString());

            var result = rpc.GetConnectionCount();
            Assert.AreEqual(9, result);
        }

        [TestMethod]
        public void TestGetContractState()
        {
            byte[] script;
            using (var sb = new ScriptBuilder())
            {
                sb.EmitSysCall(InteropService.System_Runtime_GetInvocationCounter);
                script = sb.ToArray();
            }

            ContractState state = new ContractState
            {
                Script = new byte[] { (byte)OpCode.DROP, (byte)OpCode.DROP }.Concat(script).ToArray(),
                Manifest = ContractManifest.CreateDefault(UInt160.Parse("0xa400ff00ff00ff00ff00ff00ff00ff00ff00ff01"))
            };

            JObject response = CreateResponse(1);
            response["result"] = state.ToJson();
            MockResponse(response.ToString());

            var result = rpc.GetContractState("17694b31cc7ee215cea2ded146e0b2b28768fc46");

            Assert.AreEqual(state.Script.ToHexString(), result.Script.ToHexString());
            Assert.AreEqual(state.Manifest.Abi.EntryPoint.Name, result.Manifest.Abi.EntryPoint.Name);
        }

        [TestMethod]
        public void TestGetPeers()
        {
            JObject response = CreateResponse(1);
            response["result"] = JObject.Parse(@"{
                                                    ""unconnected"": [
                                                        {
                                                            ""address"": ""::ffff:70.73.16.236"",
                                                            ""port"": 10333
                                                        },
                                                        {
                                                            ""address"": ""::ffff:82.95.77.148"",
                                                            ""port"": 10333
                                                        },
                                                        {
                                                            ""address"": ""::ffff:49.50.215.166"",
                                                            ""port"": 10333
                                                        }
                                                    ],
                                                    ""bad"": [],
                                                    ""connected"": [
                                                        {
                                                            ""address"": ""::ffff:139.219.106.33"",
                                                            ""port"": 10333
                                                        },
                                                        {
                                                            ""address"": ""::ffff:47.88.53.224"",
                                                            ""port"": 10333
                                                        }
                                                    ]
                                                }");
            MockResponse(response.ToString());

            var result = rpc.GetPeers();
            Assert.AreEqual("::ffff:139.219.106.33", result.Connected[0].Address);
            Assert.AreEqual("::ffff:82.95.77.148", result.Unconnected[1].Address);
        }

        [TestMethod]
        public void TestGetRawMempool()
        {
            JObject response = CreateResponse(1);
            response["result"] = JObject.Parse(@"[
                                                    ""0x9786cce0dddb524c40ddbdd5e31a41ed1f6b5c8a683c122f627ca4a007a7cf4e"",
                                                    ""0xb488ad25eb474f89d5ca3f985cc047ca96bc7373a6d3da8c0f192722896c1cd7"",
                                                    ""0xf86f6f2c08fbf766ebe59dc84bc3b8829f1053f0a01deb26bf7960d99fa86cd6""
                                                ]");
            MockResponse(response.ToString());

            var result = rpc.GetRawMempool();
            Assert.AreEqual("0xb488ad25eb474f89d5ca3f985cc047ca96bc7373a6d3da8c0f192722896c1cd7", result[1]);
        }

        [TestMethod]
        public void TestGetRawMempoolBoth()
        {
            JObject json = new JObject();
            json["height"] = 65535;
            json["verified"] = new JArray(new[] { "0x9786cce0dddb524c40ddbdd5e31a41ed1f6b5c8a683c122f627ca4a007a7cf4e" }.Select(p => (JObject)p));
            json["unverified"] = new JArray(new[] { "0xb488ad25eb474f89d5ca3f985cc047ca96bc7373a6d3da8c0f192722896c1cd7", "0xf86f6f2c08fbf766ebe59dc84bc3b8829f1053f0a01deb26bf7960d99fa86cd6" }.Select(p => (JObject)p));

            JObject response = CreateResponse(1);
            response["result"] = json;
            MockResponse(response.ToString());

            var result = rpc.GetRawMempoolBoth();
            Assert.AreEqual(65535u, result.Height);
            Assert.AreEqual("0x9786cce0dddb524c40ddbdd5e31a41ed1f6b5c8a683c122f627ca4a007a7cf4e", result.Verified[0]);
            Assert.AreEqual("0xf86f6f2c08fbf766ebe59dc84bc3b8829f1053f0a01deb26bf7960d99fa86cd6", result.UnVerified[1]);
        }

        [TestMethod]
        public void TestGetRawTransactionHex()
        {
            var json = TestUtils.GetTransaction().ToArray().ToHexString();

            JObject response = CreateResponse(1);
            response["result"] = json;
            MockResponse(response.ToString());

            //var result = rpc.GetRawTransactionHex("0x9786cce0dddb524c40ddbdd5e31a41ed1f6b5c8a683c122f627ca4a007a7cf4e");
            var result = rpc.GetRawTransactionHex(TestUtils.GetTransaction().Hash.ToString());
            Assert.AreEqual(json, result);
        }

        [TestMethod]
        public void TestGetRawTransaction()
        {
            var transaction = TestUtils.GetTransaction();
            JObject json = transaction.ToJson();
            JObject response = CreateResponse(1);
            response["result"] = json;
            MockResponse(response.ToString());

            var result = rpc.GetRawTransaction("0x9786cce0dddb524c40ddbdd5e31a41ed1f6b5c8a683c122f627ca4a007a7cf4e");
            Assert.AreEqual(transaction.Hash, result.Transaction.Hash);
            Assert.AreEqual(json.ToString(), result.ToJson().ToString());

            // make the code compatible with the old version
            json["blockhash"] = UInt256.Zero.ToString();
            json["confirmations"] = 100;
            json["blocktime"] = 10;
            MockResponse(response.ToString());

            result = rpc.GetRawTransaction("0x9786cce0dddb524c40ddbdd5e31a41ed1f6b5c8a683c122f627ca4a007a7cf4e");
            Assert.AreEqual(transaction.Hash, result.Transaction.Hash);
            Assert.AreEqual(100, result.Confirmations);
            Assert.AreEqual(null, result.VMState);
            Assert.AreEqual(json.ToString(), result.ToJson().ToString());

            json["vmState"] = VMState.HALT;
            MockResponse(response.ToString());

            result = rpc.GetRawTransaction("0x9786cce0dddb524c40ddbdd5e31a41ed1f6b5c8a683c122f627ca4a007a7cf4e");
            Assert.AreEqual(transaction.Hash, result.Transaction.Hash);
            Assert.AreEqual(100, result.Confirmations);
            Assert.AreEqual(VMState.HALT, result.VMState);
            Assert.AreEqual(json.ToString(), result.ToJson().ToString());
        }

        [TestMethod]
        public void TestGetStorage()
        {
            JObject json = "4c696e";
            JObject response = CreateResponse(1);
            response["result"] = json;
            MockResponse(response.ToString());

            var result = rpc.GetStorage("03febccf81ac85e3d795bc5cbd4e84e907812aa3", "5065746572");
            Assert.AreEqual("4c696e", result);
        }

        [TestMethod]
        public void TestGetTransactionHeight()
        {
            JObject json = 10000;
            JObject response = CreateResponse(1);
            response["result"] = json;
            MockResponse(response.ToString());

            var result = rpc.GetTransactionHeight("9c909e1e3ba03290553a68d862e002c7a21ba302e043fc492fe069bf6a134d29");
            Assert.AreEqual(json.ToString(), result.ToString());
        }

        [TestMethod]
        public void TestGetValidators()
        {
            JObject json = JObject.Parse(@"[
                                                {
                                                    ""publickey"": ""02486fd15702c4490a26703112a5cc1d0923fd697a33406bd5a1c00e0013b09a70"",
                                                    ""votes"": ""46632420"",
                                                    ""active"": true
                                                },
                                                {
                                                    ""publickey"": ""024c7b7fb6c310fccf1ba33b082519d82964ea93868d676662d4a59ad548df0e7d"",
                                                    ""votes"": ""46632420"",
                                                    ""active"": true
                                                }
                                            ]");
            JObject response = CreateResponse(1);
            response["result"] = json;
            MockResponse(response.ToString());

            var result = rpc.GetValidators();
            Assert.AreEqual(((JArray)json)[0].ToString(), (result[0]).ToJson().ToString());
        }

        [TestMethod]
        public void TestGetVersion()
        {
            JObject json = new JObject();
            json["tcpPort"] = 30001;
            json["wsPort"] = 30002;
            json["nonce"] = 1546258664;
            json["useragent"] = "/NEO:2.7.5/";

            var json1 = JObject.Parse(@"{
                                            ""tcpPort"": 30001,
                                            ""wsPort"": 30002,
                                            ""nonce"": 1546258664,
                                            ""useragent"": ""/NEO:2.7.5/""
                                        }");
            Assert.AreEqual(json.ToString(), json1.ToString());

            JObject response = CreateResponse(1);
            response["result"] = json;
            MockResponse(response.ToString());

            var result = rpc.GetVersion();
            Assert.AreEqual(30001, result.TcpPort);
            Assert.AreEqual("/NEO:2.7.5/", result.UserAgent);
        }

        [TestMethod]
        public void TestInvokeFunction()
        {
            JObject json = JObject.Parse(@"
            {
                ""script"": ""1426ae7c6c9861ec418468c1f0fdc4a7f2963eb89151c10962616c616e63654f6667be39e7b562f60cbfe2aebca375a2e5ee28737caf"",
                ""state"": ""HALT"",
                ""gas_consumed"": ""0.311"",
                ""stack"": [
                    {
                        ""type"": ""ByteArray"",
                        ""value"": ""262bec084432""
                    }
                ]
            }");
            JObject response = CreateResponse(1);
            response["result"] = json;
            MockResponse(response.ToString());

            var result = rpc.InvokeFunction("af7c7328eee5a275a3bcaee2bf0cf662b5e739be", "balanceOf", new[] { new RpcStack { Type = "Hash160", Value = "91b83e96f2a7c4fdf0c1688441ec61986c7cae26" } });
            Assert.AreEqual(json.ToString(), result.ToJson().ToString());
        }

        [TestMethod]
        public void TestInvokeScript()
        {
            JObject json = JObject.Parse(@"
            {
                ""script"": ""1426ae7c6c9861ec418468c1f0fdc4a7f2963eb89151c10962616c616e63654f6667be39e7b562f60cbfe2aebca375a2e5ee28737caf"",
                ""state"": ""HALT"",
                ""gas_consumed"": ""0.311"",
                ""stack"": [
                    {
                        ""type"": ""ByteArray"",
                        ""value"": ""262bec084432""
                    }
                ]
            }");
            JObject response = CreateResponse(1);
            response["result"] = json;
            MockResponse(response.ToString());

            var result = rpc.InvokeScript("00046e616d656724058e5e1b6008847cd662728549088a9ee82191".HexToBytes());
            Assert.AreEqual(json.ToString(), result.ToJson().ToString());
        }

        [TestMethod]
        public void TestListPlugins()
        {
            JObject json = JObject.Parse(@"[{
                ""name"": ""SimplePolicyPlugin"",
                ""version"": ""2.10.1.0"",
                ""interfaces"": [
                    ""ILogPlugin"",
                    ""IPolicyPlugin""
                ]
            }]");
            JObject response = CreateResponse(1);
            response["result"] = json;
            MockResponse(response.ToString());

            var result = rpc.ListPlugins();
            Assert.AreEqual(((JArray)json)[0].ToString(), result[0].ToJson().ToString());
        }

        [TestMethod]
        public void TestSendRawTransaction()
        {
            var json = new JObject();
            json["hash"] = UInt256.Zero.ToString();
            JObject response = CreateResponse(1);
            response["result"] = json;
            MockResponse(response.ToString());

            var result = rpc.SendRawTransaction("80000001195876cb34364dc38b730077156c6bc3a7fc570044a66fbfeeea56f71327e8ab0000029b7cffdaa674beae0f930ebe6085af9093e5fe56b34a5c220ccdcf6efc336fc500c65eaf440000000f9a23e06f74cf86b8827a9108ec2e0f89ad956c9b7cffdaa674beae0f930ebe6085af9093e5fe56b34a5c220ccdcf6efc336fc50092e14b5e00000030aab52ad93f6ce17ca07fa88fc191828c58cb71014140915467ecd359684b2dc358024ca750609591aa731a0b309c7fb3cab5cd0836ad3992aa0a24da431f43b68883ea5651d548feb6bd3c8e16376e6e426f91f84c58232103322f35c7819267e721335948d385fae5be66e7ba8c748ac15467dcca0693692dac".HexToBytes());
            Assert.AreEqual(UInt256.Zero.ToString(), result);
        }

        [TestMethod]
        public void TestSubmitBlock()
        {
            var json = new JObject();
            json["hash"] = UInt256.Zero.ToString();
            JObject response = CreateResponse(1);
            response["result"] = json;
            MockResponse(response.ToString());

            var result = rpc.SubmitBlock("03febccf81ac85e3d795bc5cbd4e84e907812aa3".HexToBytes());
            Assert.AreEqual(UInt256.Zero.ToString(), result);
        }

        [TestMethod]
        public void TestValidateAddress()
        {
            JObject json = new JObject();
            json["address"] = "AQVh2pG732YvtNaxEGkQUei3YA4cvo7d2i";
            json["isvalid"] = false;
            JObject response = CreateResponse(1);
            response["result"] = json;
            MockResponse(response.ToString());

            var result = rpc.ValidateAddress("AQVh2pG732YvtNaxEGkQUei3YA4cvo7d2i");
            Assert.AreEqual(json.ToString(), result.ToJson().ToString());
        }

        [TestMethod()]
        public void GetApplicationLogTest()
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
            JObject response = CreateResponse(1);
            response["result"] = json;
            MockResponse(response.ToString());

            var result = rpc.GetApplicationLog("00046e616d656724058e5e1b6008847cd662728549088a9ee82191");
            Assert.AreEqual(json.ToString(), result.ToJson().ToString());
        }

        [TestMethod()]
        public void GetNep5TransfersTest()
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
                    },
                    {
                        ""timestamp"": 1554880287,
                        ""asset_hash"": ""0x1aada0032aba1ef6d1f07bbd8bec1d85f5380fb3"",
                        ""transfer_address"": ""NaEVDyZ5aivkKaH6PrmLe7e1xhJViQHQz5"",
                        ""amount"": ""100000000000"",
                        ""block_index"": 397769,
                        ""transfer_notify_index"": 0,
                        ""tx_hash"": ""0x12fdf7ce8b2388d23ab223854cb29e5114d8288c878de23b7924880f82dfc834""
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
            JObject response = CreateResponse(1);
            response["result"] = json;
            MockResponse(response.ToString());

            var result = rpc.GetNep5Transfers("NaEVDyZ5aivkKaH6PrmLe7e1xhJViQHQz5");
            Assert.AreEqual(json.ToString(), result.ToJson().ToString());
        }

        [TestMethod()]
        public void GetNep5BalancesTest()
        {
            JObject json = JObject.Parse(@"
            {
            ""balance"": [
                {
                    ""asset_hash"": ""0xa48b6e1291ba24211ad11bb90ae2a10bf1fcd5a8"",
                    ""amount"": ""50000000000"",
                    ""last_updated_block"": 251604
                },
                {
                    ""asset_hash"": ""0x1aada0032aba1ef6d1f07bbd8bec1d85f5380fb3"",
                    ""amount"": ""50000000000"",
                    ""last_updated_block"": 251600
                }
            ],
            ""address"": ""NaEVDyZ5aivkKaH6PrmLe7e1xhJViQHQz5""
        }");
            JObject response = CreateResponse(1);
            response["result"] = json;
            MockResponse(response.ToString());

            var result = rpc.GetNep5Balances("AbHgdBaWEnHkCiLtDZXjhvhaAK2cwFh5pF");
            Assert.AreEqual(json.ToString(), result.ToJson().ToString());
        }
    }
}
