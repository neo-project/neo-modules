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
            var httpClient = new HttpClient(handlerMock.Object);
            rpc = new RpcClient(httpClient, "http://seed1.neo.org:10331");
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
        public async Task TestErrorResponse()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == (nameof(rpc.SendRawTransaction) + "error").ToLower());
            try
            {
                var result = await rpc.SendRawTransaction(test.Request.Params[0].AsString().HexToBytes().AsSerializable<Transaction>());
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
        public async Task TestGetBestBlockHash()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetBestBlockHash).ToLower());
            var result = await rpc.GetBestBlockHash();
            Assert.AreEqual(test.Response.Result.AsString(), result);
        }

        [TestMethod]
        public async Task TestGetBlockHex()
        {
            var tests = TestUtils.RpcTestCases.Where(p => p.Name == nameof(rpc.GetBlockHex).ToLower());
            foreach (var test in tests)
            {
                var result = await rpc.GetBlockHex(test.Request.Params[0].AsString());
                Assert.AreEqual(test.Response.Result.AsString(), result);
            }
        }

        [TestMethod]
        public async Task TestGetBlock()
        {
            var tests = TestUtils.RpcTestCases.Where(p => p.Name == nameof(rpc.GetBlock).ToLower());
            foreach (var test in tests)
            {
                var result = await rpc.GetBlock(test.Request.Params[0].AsString());
                Assert.AreEqual(test.Response.Result.AsString(), result.ToJson().ToString());
            }
        }

        [TestMethod]
        public async Task TestGetBlockCount()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetBlockCount).ToLower());
            var result = await rpc.GetBlockCount();
            Assert.AreEqual(test.Response.Result.AsString(), result.ToString());
        }

        [TestMethod]
        public async Task TestGetBlockHash()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetBlockHash).ToLower());
            var result = await rpc.GetBlockHash((int)test.Request.Params[0].AsNumber());
            Assert.AreEqual(test.Response.Result.AsString(), result.ToString());
        }

        [TestMethod]
        public async Task TestGetBlockHeaderHex()
        {
            var tests = TestUtils.RpcTestCases.Where(p => p.Name == nameof(rpc.GetBlockHeaderHex).ToLower());
            foreach (var test in tests)
            {
                var result = await rpc.GetBlockHeaderHex(test.Request.Params[0].AsString());
                Assert.AreEqual(test.Response.Result.AsString(), result);
            }
        }

        [TestMethod]
        public async Task TestGetBlockHeader()
        {
            var tests = TestUtils.RpcTestCases.Where(p => p.Name == nameof(rpc.GetBlockHeader).ToLower());
            foreach (var test in tests)
            {
                var result = await rpc.GetBlockHeader(test.Request.Params[0].AsString());
                Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
            }
        }

        [TestMethod]
        public async Task TestGetContractState()
        {
            var tests = TestUtils.RpcTestCases.Where(p => p.Name == nameof(rpc.GetContractState).ToLower());
            foreach (var test in tests)
            {
                var result = await rpc.GetContractState(test.Request.Params[0].AsString());
                Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
            }
        }

        [TestMethod]
        public async Task TestGetRawMempool()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetRawMempool).ToLower());
            var result = await rpc.GetRawMempool();
            Assert.AreEqual(test.Response.Result.ToString(), ((JArray)result.Select(p => (JObject)p).ToArray()).ToString());
        }

        [TestMethod]
        public async Task TestGetRawMempoolBoth()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetRawMempoolBoth).ToLower());
            var result = await rpc.GetRawMempoolBoth();
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        [TestMethod]
        public async Task TestGetRawTransactionHex()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetRawTransactionHex).ToLower());
            var result = await rpc.GetRawTransactionHex(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result.AsString(), result);
        }

        [TestMethod]
        public async Task TestGetRawTransaction()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetRawTransaction).ToLower());
            var result = await rpc.GetRawTransaction(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        [TestMethod]
        public async Task TestGetStorage()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetStorage).ToLower());
            var result = await rpc.GetStorage(test.Request.Params[0].AsString(), test.Request.Params[1].AsString());
            Assert.AreEqual(test.Response.Result.AsString(), result);
        }

        [TestMethod]
        public async Task TestGetTransactionHeight()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetTransactionHeight).ToLower());
            var result = await rpc.GetTransactionHeight(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToString());
        }

        [TestMethod]
        public async Task TestGetValidators()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetValidators).ToLower());
            var result = await rpc.GetValidators();
            Assert.AreEqual(test.Response.Result.ToString(), ((JArray)result.Select(p => p.ToJson()).ToArray()).ToString());
        }

        #endregion Blockchain

        #region Node

        [TestMethod]
        public async Task TestGetConnectionCount()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetConnectionCount).ToLower());
            var result = await rpc.GetConnectionCount();
            Assert.AreEqual(test.Response.Result.ToString(), result.ToString());
        }

        [TestMethod]
        public async Task TestGetPeers()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetPeers).ToLower());
            var result = await rpc.GetPeers();
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        [TestMethod]
        public async Task TestGetVersion()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetVersion).ToLower());
            var result = await rpc.GetVersion();
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        [TestMethod]
        public async Task TestSendRawTransaction()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.SendRawTransaction).ToLower());
            var result = await rpc.SendRawTransaction(test.Request.Params[0].AsString().HexToBytes().AsSerializable<Transaction>());
            Assert.AreEqual(test.Response.Result["hash"].AsString(), result.ToString());
        }

        [TestMethod]
        public async Task TestSubmitBlock()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.SubmitBlock).ToLower());
            var block = TestUtils.GetBlock(2).Hash;
            var result = await rpc.SubmitBlock(test.Request.Params[0].AsString().HexToBytes());
            Assert.AreEqual(test.Response.Result["hash"].AsString(), result.ToString());
        }

        #endregion Node

        #region SmartContract

        [TestMethod]
        public async Task TestInvokeFunction()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.InvokeFunction).ToLower());
            var result = await rpc.InvokeFunction(test.Request.Params[0].AsString(), test.Request.Params[1].AsString(),
                ((JArray)test.Request.Params[2]).Select(p => RpcStack.FromJson(p)).ToArray());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        [TestMethod]
        public async Task TestInvokeScript()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.InvokeScript).ToLower());
            var result = await rpc.InvokeScript(test.Request.Params[0].AsString().HexToBytes());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        [TestMethod]
        public async Task TestGetUnclaimedGas()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetUnclaimedGas).ToLower());
            var result = await rpc.GetUnclaimedGas(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result["unclaimed"].AsString(), result.Unclaimed.ToString());
        }

        #endregion SmartContract

        #region Utilities

        [TestMethod]
        public async Task TestListPlugins()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.ListPlugins).ToLower());
            var result = await rpc.ListPlugins();
            Assert.AreEqual(test.Response.Result.ToString(), ((JArray)result.Select(p => p.ToJson()).ToArray()).ToString());
        }

        [TestMethod]
        public async Task TestValidateAddress()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.ValidateAddress).ToLower());
            var result = await rpc.ValidateAddress(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        #endregion Utilities

        #region Wallet

        [TestMethod]
        public async Task TestCloseWallet()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.CloseWallet).ToLower());
            var result = await rpc.CloseWallet();
            Assert.AreEqual(test.Response.Result.AsBoolean(), result);
        }

        [TestMethod]
        public async Task TestDumpPrivKey()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.DumpPrivKey).ToLower());
            var result = await rpc.DumpPrivKey(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result.AsString(), result);
        }

        [TestMethod]
        public async Task TestGetNewAddress()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetNewAddress).ToLower());
            var result = await rpc.GetNewAddress();
            Assert.AreEqual(test.Response.Result.AsString(), result);
        }

        [TestMethod]
        public async Task TestGetWalletBalance()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetWalletBalance).ToLower());
            var result = await rpc.GetWalletBalance(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result["balance"].AsString(), result.Value.ToString());
        }

        [TestMethod]
        public async Task TestGetWalletUnclaimedGas()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetWalletUnclaimedGas).ToLower());
            var result = await rpc.GetWalletUnclaimedGas();
            Assert.AreEqual(test.Response.Result.AsString(), result.ToString());
        }

        [TestMethod]
        public async Task TestImportPrivKey()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.ImportPrivKey).ToLower());
            var result = await rpc.ImportPrivKey(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        [TestMethod]
        public async Task TestListAddress()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.ListAddress).ToLower());
            var result = await rpc.ListAddress();
            Assert.AreEqual(test.Response.Result.ToString(), ((JArray)result.Select(p => p.ToJson()).ToArray()).ToString());
        }

        [TestMethod]
        public async Task TestOpenWallet()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.OpenWallet).ToLower());
            var result = await rpc.OpenWallet(test.Request.Params[0].AsString(), test.Request.Params[1].AsString());
            Assert.AreEqual(test.Response.Result.AsBoolean(), result);
        }

        [TestMethod]
        public async Task TestSendFrom()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.SendFrom).ToLower());
            var result = await rpc.SendFrom(test.Request.Params[0].AsString(), test.Request.Params[1].AsString(),
                test.Request.Params[2].AsString(), test.Request.Params[3].AsString());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToString());
        }

        [TestMethod]
        public async Task TestSendMany()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.SendMany).ToLower());
            var result = await rpc.SendMany(test.Request.Params[0].AsString(), ((JArray)test.Request.Params[1]).Select(p => RpcTransferOut.FromJson(p)));
            Assert.AreEqual(test.Response.Result.ToString(), result.ToString());
        }

        [TestMethod]
        public async Task TestSendToAddress()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.SendToAddress).ToLower());
            var result = await rpc.SendToAddress(test.Request.Params[0].AsString(), test.Request.Params[1].AsString(), test.Request.Params[2].AsString());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToString());
        }

        #endregion Wallet

        #region Plugins

        [TestMethod()]
        public async Task GetApplicationLogTest()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetApplicationLog).ToLower());
            var result = await rpc.GetApplicationLog(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        [TestMethod()]
        public async Task GetNep5TransfersTest()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetNep5Transfers).ToLower());
            var result = await rpc.GetNep5Transfers(test.Request.Params[0].AsString(), (ulong)test.Request.Params[1].AsNumber(),
                (ulong)test.Request.Params[2].AsNumber());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        [TestMethod()]
        public async Task GetNep5BalancesTest()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetNep5Balances).ToLower());
            var result = await rpc.GetNep5Balances(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        #endregion Plugins
    }
}
