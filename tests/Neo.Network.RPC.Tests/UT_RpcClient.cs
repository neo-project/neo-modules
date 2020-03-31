using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using Neo.IO;
using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC.Models;
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
            foreach (var test in TestUtils.RpcTestCases)
            {
                MockResponse(test.Request, test.Response);
            }
        }

        private void MockResponse(RpcRequest request, RpcResponse response)
        {
            handlerMock.Protected()
               // Setup the PROTECTED method to mock
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.Is<HttpRequestMessage>(p => p.Content.ReadAsStringAsync().Result == request.ToJson().ToString()),
                  ItExpr.IsAny<CancellationToken>()
               )
               // prepare the expected response of the mocked http call
               .ReturnsAsync(new HttpResponseMessage()
               {
                   StatusCode = HttpStatusCode.OK,
                   Content = new StringContent(response.ToJson().ToString()),
               })
               .Verifiable();
        }

        [TestMethod]
        public void TestErrorResponse()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == (nameof(rpc.SendRawTransaction) + "error").ToLower());
            try
            {
                var result = rpc.SendRawTransaction(test.Request.Params[0].AsString().HexToBytes().AsSerializable<Transaction>());
            }
            catch (RpcException ex)
            {
                Assert.AreEqual(-500, ex.HResult);
                Assert.AreEqual("InsufficientFunds", ex.Message);
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

        #region Blockchain

        [TestMethod]
        public void TestGetBestBlockHash()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetBestBlockHash).ToLower());
            var result = rpc.GetBestBlockHash();
            Assert.AreEqual(test.Response.Result.AsString(), result);
        }

        [TestMethod]
        public void TestGetBlockHex()
        {
            var tests = TestUtils.RpcTestCases.Where(p => p.Name == nameof(rpc.GetBlockHex).ToLower());
            foreach (var test in tests)
            {
                var result = rpc.GetBlockHex(test.Request.Params[0].AsString());
                Assert.AreEqual(test.Response.Result.AsString(), result);
            }
        }

        [TestMethod]
        public void TestGetBlock()
        {
            var tests = TestUtils.RpcTestCases.Where(p => p.Name == nameof(rpc.GetBlock).ToLower());
            foreach (var test in tests)
            {
                var result = rpc.GetBlock(test.Request.Params[0].AsString());
                Assert.AreEqual(test.Response.Result.AsString(), result.ToJson().ToString());
            }
        }

        [TestMethod]
        public void TestGetBlockCount()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetBlockCount).ToLower());
            var result = rpc.GetBlockCount();
            Assert.AreEqual(test.Response.Result.AsString(), result.ToString());
        }

        [TestMethod]
        public void TestGetBlockHash()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetBlockHash).ToLower());
            var result = rpc.GetBlockHash((int)test.Request.Params[0].AsNumber());
            Assert.AreEqual(test.Response.Result.AsString(), result.ToString());
        }

        [TestMethod]
        public void TestGetBlockHeaderHex()
        {
            var tests = TestUtils.RpcTestCases.Where(p => p.Name == nameof(rpc.GetBlockHeaderHex).ToLower());
            foreach (var test in tests)
            {
                var result = rpc.GetBlockHeaderHex(test.Request.Params[0].AsString());
                Assert.AreEqual(test.Response.Result.AsString(), result);
            }
        }

        [TestMethod]
        public void TestGetBlockHeader()
        {
            var tests = TestUtils.RpcTestCases.Where(p => p.Name == nameof(rpc.GetBlockHeader).ToLower());
            foreach (var test in tests)
            {
                var result = rpc.GetBlockHeader(test.Request.Params[0].AsString());
                Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
            }
        }

        [TestMethod]
        public void TestGetContractState()
        {
            var tests = TestUtils.RpcTestCases.Where(p => p.Name == nameof(rpc.GetContractState).ToLower());
            foreach (var test in tests)
            {
                var result = rpc.GetContractState(test.Request.Params[0].AsString());
                Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
            }
        }

        [TestMethod]
        public void TestGetRawMempool()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetRawMempool).ToLower());
            var result = rpc.GetRawMempool();
            Assert.AreEqual(test.Response.Result.ToString(), ((JArray)result.Select(p => (JObject)p).ToArray()).ToString());
        }

        [TestMethod]
        public void TestGetRawMempoolBoth()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetRawMempoolBoth).ToLower());
            var result = rpc.GetRawMempoolBoth();
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        [TestMethod]
        public void TestGetRawTransactionHex()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetRawTransactionHex).ToLower());
            var result = rpc.GetRawTransactionHex(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result.AsString(), result);
        }

        [TestMethod]
        public void TestGetRawTransaction()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetRawTransaction).ToLower());
            var result = rpc.GetRawTransaction(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        [TestMethod]
        public void TestGetStorage()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetStorage).ToLower());
            var result = rpc.GetStorage(test.Request.Params[0].AsString(), test.Request.Params[1].AsString());
            Assert.AreEqual(test.Response.Result.AsString(), result);
        }

        [TestMethod]
        public void TestGetTransactionHeight()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetTransactionHeight).ToLower());
            var result = rpc.GetTransactionHeight(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToString());
        }

        [TestMethod]
        public void TestGetValidators()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetValidators).ToLower());
            var result = rpc.GetValidators();
            Assert.AreEqual(test.Response.Result.ToString(), ((JArray)result.Select(p => p.ToJson()).ToArray()).ToString());
        }

        #endregion Blockchain

        #region Node

        [TestMethod]
        public void TestGetConnectionCount()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetConnectionCount).ToLower());
            var result = rpc.GetConnectionCount();
            Assert.AreEqual(test.Response.Result.ToString(), result.ToString());
        }

        [TestMethod]
        public void TestGetPeers()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetPeers).ToLower());
            var result = rpc.GetPeers();
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        [TestMethod]
        public void TestGetVersion()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetVersion).ToLower());
            var result = rpc.GetVersion();
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        [TestMethod]
        public void TestSendRawTransaction()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.SendRawTransaction).ToLower());
            var result = rpc.SendRawTransaction(test.Request.Params[0].AsString().HexToBytes().AsSerializable<Transaction>());
            Assert.AreEqual(test.Response.Result["hash"].AsString(), result.ToString());
        }

        [TestMethod]
        public void TestSubmitBlock()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.SubmitBlock).ToLower());
            var block = TestUtils.GetBlock(2).Hash;
            var result = rpc.SubmitBlock(test.Request.Params[0].AsString().HexToBytes());
            Assert.AreEqual(test.Response.Result["hash"].AsString(), result.ToString());
        }

        #endregion Node

        #region SmartContract

        [TestMethod]
        public void TestInvokeFunction()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.InvokeFunction).ToLower());
            var result = rpc.InvokeFunction(test.Request.Params[0].AsString(), test.Request.Params[1].AsString(),
                ((JArray)test.Request.Params[2]).Select(p => RpcStack.FromJson(p)).ToArray());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        [TestMethod]
        public void TestInvokeScript()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.InvokeScript).ToLower());
            var result = rpc.InvokeScript(test.Request.Params[0].AsString().HexToBytes());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        #endregion SmartContract

        #region Utilities

        [TestMethod]
        public void TestListPlugins()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.ListPlugins).ToLower());
            var result = rpc.ListPlugins();
            Assert.AreEqual(test.Response.Result.ToString(), ((JArray)result.Select(p => p.ToJson()).ToArray()).ToString());
        }

        [TestMethod]
        public void TestValidateAddress()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.ValidateAddress).ToLower());
            var result = rpc.ValidateAddress(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        #endregion Utilities

        #region Wallet

        [TestMethod]
        public void TestCloseWallet()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.CloseWallet).ToLower());
            var result = rpc.CloseWallet();
            Assert.AreEqual(test.Response.Result.AsBoolean(), result);
        }

        [TestMethod]
        public void TestDumpPrivKey()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.DumpPrivKey).ToLower());
            var result = rpc.DumpPrivKey(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result.AsString(), result);
        }

        [TestMethod]
        public void TestGetBalance()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetBalance).ToLower());
            var result = rpc.GetBalance(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result["balance"].AsString(), result.Value.ToString());
        }

        [TestMethod]
        public void TestGetNewAddress()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetNewAddress).ToLower());
            var result = rpc.GetNewAddress();
            Assert.AreEqual(test.Response.Result.AsString(), result);
        }

        [TestMethod]
        public void TestGetUnclaimedGas()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetUnclaimedGas).ToLower());
            var result = rpc.GetUnclaimedGas();
            Assert.AreEqual(test.Response.Result.AsString(), result.ToString());
        }

        [TestMethod]
        public void TestImportPrivKey()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.ImportPrivKey).ToLower());
            var result = rpc.ImportPrivKey(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        [TestMethod]
        public void TestListAddress()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.ListAddress).ToLower());
            var result = rpc.ListAddress();
            Assert.AreEqual(test.Response.Result.ToString(), ((JArray)result.Select(p => p.ToJson()).ToArray()).ToString());
        }

        [TestMethod]
        public void TestOpenWallet()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.OpenWallet).ToLower());
            var result = rpc.OpenWallet(test.Request.Params[0].AsString(), test.Request.Params[1].AsString());
            Assert.AreEqual(test.Response.Result.AsBoolean(), result);
        }

        [TestMethod]
        public void TestSendFrom()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.SendFrom).ToLower());
            var result = rpc.SendFrom(test.Request.Params[0].AsString(), test.Request.Params[1].AsString(),
                test.Request.Params[2].AsString(), test.Request.Params[3].AsString());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToString());
        }

        [TestMethod]
        public void TestSendMany()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.SendMany).ToLower());
            var result = rpc.SendMany(test.Request.Params[0].AsString(), ((JArray)test.Request.Params[1]).Select(p => RpcTransferOut.FromJson(p)));
            Assert.AreEqual(test.Response.Result.ToString(), result.ToString());
        }

        [TestMethod]
        public void TestSendToAddress()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.SendToAddress).ToLower());
            var result = rpc.SendToAddress(test.Request.Params[0].AsString(), test.Request.Params[1].AsString(), test.Request.Params[2].AsString());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToString());
        }

        #endregion Wallet

        #region Plugins

        [TestMethod()]
        public void GetApplicationLogTest()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetApplicationLog).ToLower());
            var result = rpc.GetApplicationLog(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        [TestMethod()]
        public void GetNep5TransfersTest()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetNep5Transfers).ToLower());
            var result = rpc.GetNep5Transfers(test.Request.Params[0].AsString(), (ulong)test.Request.Params[1].AsNumber(),
                (ulong)test.Request.Params[2].AsNumber());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        [TestMethod()]
        public void GetNep5BalancesTest()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetNep5Balances).ToLower());
            var result = rpc.GetNep5Balances(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        #endregion Plugins
    }
}
