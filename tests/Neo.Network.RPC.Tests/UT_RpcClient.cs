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
            var test = TestUtils.RpcTestCases.Find(p => p.Name == (nameof(rpc.SendRawTransactionAsync) + "error").ToLower());
            try
            {
                var result = await rpc.SendRawTransactionAsync(test.Request.Params[0].AsString().HexToBytes().AsSerializable<Transaction>());
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
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetBestBlockHashAsync).ToLower());
            var result = await rpc.GetBestBlockHashAsync();
            Assert.AreEqual(test.Response.Result.AsString(), result);
        }

        [TestMethod]
        public async Task TestGetBlockHex()
        {
            var tests = TestUtils.RpcTestCases.Where(p => p.Name == nameof(rpc.GetBlockHexAsync).ToLower());
            foreach (var test in tests)
            {
                var result = await rpc.GetBlockHexAsync(test.Request.Params[0].AsString());
                Assert.AreEqual(test.Response.Result.AsString(), result);
            }
        }

        [TestMethod]
        public async Task TestGetBlock()
        {
            var tests = TestUtils.RpcTestCases.Where(p => p.Name == nameof(rpc.GetBlockAsync).ToLower());
            foreach (var test in tests)
            {
                var result = await rpc.GetBlockAsync(test.Request.Params[0].AsString());
                Assert.AreEqual(test.Response.Result.AsString(), result.ToJson().ToString());
            }
        }

        [TestMethod]
        public async Task TestGetBlockCount()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetBlockCountAsync).ToLower());
            var result = await rpc.GetBlockCountAsync();
            Assert.AreEqual(test.Response.Result.AsString(), result.ToString());
        }

        [TestMethod]
        public async Task TestGetBlockHash()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetBlockHashAsync).ToLower());
            var result = await rpc.GetBlockHashAsync((int)test.Request.Params[0].AsNumber());
            Assert.AreEqual(test.Response.Result.AsString(), result.ToString());
        }

        [TestMethod]
        public async Task TestGetBlockHeaderHex()
        {
            var tests = TestUtils.RpcTestCases.Where(p => p.Name == nameof(rpc.GetBlockHeaderHexAsync).ToLower());
            foreach (var test in tests)
            {
                var result = await rpc.GetBlockHeaderHexAsync(test.Request.Params[0].AsString());
                Assert.AreEqual(test.Response.Result.AsString(), result);
            }
        }

        [TestMethod]
        public async Task TestGetBlockHeader()
        {
            var tests = TestUtils.RpcTestCases.Where(p => p.Name == nameof(rpc.GetBlockHeaderAsync).ToLower());
            foreach (var test in tests)
            {
                var result = await rpc.GetBlockHeaderAsync(test.Request.Params[0].AsString());
                Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
            }
        }

        [TestMethod]
        public async Task TestGetContractState()
        {
            var tests = TestUtils.RpcTestCases.Where(p => p.Name == nameof(rpc.GetContractStateAsync).ToLower());
            foreach (var test in tests)
            {
                var result = await rpc.GetContractStateAsync(test.Request.Params[0].AsString());
                Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
            }
        }

        [TestMethod]
        public async Task TestGetRawMempool()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetRawMempoolAsync).ToLower());
            var result = await rpc.GetRawMempoolAsync();
            Assert.AreEqual(test.Response.Result.ToString(), ((JArray)result.Select(p => (JObject)p).ToArray()).ToString());
        }

        [TestMethod]
        public async Task TestGetRawMempoolBoth()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetRawMempoolBothAsync).ToLower());
            var result = await rpc.GetRawMempoolBothAsync();
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        [TestMethod]
        public async Task TestGetRawTransactionHex()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetRawTransactionHexAsync).ToLower());
            var result = await rpc.GetRawTransactionHexAsync(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result.AsString(), result);
        }

        [TestMethod]
        public async Task TestGetRawTransaction()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetRawTransactionAsync).ToLower());
            var result = await rpc.GetRawTransactionAsync(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        [TestMethod]
        public async Task TestGetStorage()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetStorageAsync).ToLower());
            var result = await rpc.GetStorageAsync(test.Request.Params[0].AsString(), test.Request.Params[1].AsString());
            Assert.AreEqual(test.Response.Result.AsString(), result);
        }

        [TestMethod]
        public async Task TestGetTransactionHeight()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetTransactionHeightAsync).ToLower());
            var result = await rpc.GetTransactionHeightAsync(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToString());
        }

        [TestMethod]
        public async Task TestGetNextBlockValidators()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetNextBlockValidatorsAsync).ToLower());
            var result = await rpc.GetNextBlockValidatorsAsync();
            Assert.AreEqual(test.Response.Result.ToString(), ((JArray)result.Select(p => p.ToJson()).ToArray()).ToString());
        }

        #endregion Blockchain

        #region Node

        [TestMethod]
        public async Task TestGetConnectionCount()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetConnectionCountAsync).ToLower());
            var result = await rpc.GetConnectionCountAsync();
            Assert.AreEqual(test.Response.Result.ToString(), result.ToString());
        }

        [TestMethod]
        public async Task TestGetPeers()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetPeersAsync).ToLower());
            var result = await rpc.GetPeersAsync();
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        [TestMethod]
        public async Task TestGetVersion()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetVersionAsync).ToLower());
            var result = await rpc.GetVersionAsync();
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        [TestMethod]
        public async Task TestSendRawTransaction()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.SendRawTransactionAsync).ToLower());
            var result = await rpc.SendRawTransactionAsync(test.Request.Params[0].AsString().HexToBytes().AsSerializable<Transaction>());
            Assert.AreEqual(test.Response.Result["hash"].AsString(), result.ToString());
        }

        [TestMethod]
        public async Task TestSubmitBlock()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.SubmitBlockAsync).ToLower());
            var block = TestUtils.GetBlock(2).Hash;
            var result = await rpc.SubmitBlockAsync(test.Request.Params[0].AsString().HexToBytes());
            Assert.AreEqual(test.Response.Result["hash"].AsString(), result.ToString());
        }

        #endregion Node

        #region SmartContract

        [TestMethod]
        public async Task TestInvokeFunction()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.InvokeFunctionAsync).ToLower());
            var result = await rpc.InvokeFunctionAsync(test.Request.Params[0].AsString(), test.Request.Params[1].AsString(),
                ((JArray)test.Request.Params[2]).Select(p => RpcStack.FromJson(p)).ToArray());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        [TestMethod]
        public async Task TestInvokeScript()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.InvokeScriptAsync).ToLower());
            var result = await rpc.InvokeScriptAsync(Convert.FromBase64String(test.Request.Params[0].AsString()));
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        [TestMethod]
        public async Task TestGetUnclaimedGas()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetUnclaimedGasAsync).ToLower());
            var result = await rpc.GetUnclaimedGasAsync(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result["unclaimed"].AsString(), result.Unclaimed.ToString());
        }

        #endregion SmartContract

        #region Utilities

        [TestMethod]
        public async Task TestListPlugins()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.ListPluginsAsync).ToLower());
            var result = await rpc.ListPluginsAsync();
            Assert.AreEqual(test.Response.Result.ToString(), ((JArray)result.Select(p => p.ToJson()).ToArray()).ToString());
        }

        [TestMethod]
        public async Task TestValidateAddress()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.ValidateAddressAsync).ToLower());
            var result = await rpc.ValidateAddressAsync(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        #endregion Utilities

        #region Wallet

        [TestMethod]
        public async Task TestCloseWallet()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.CloseWalletAsync).ToLower());
            var result = await rpc.CloseWalletAsync();
            Assert.AreEqual(test.Response.Result.AsBoolean(), result);
        }

        [TestMethod]
        public async Task TestDumpPrivKey()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.DumpPrivKeyAsync).ToLower());
            var result = await rpc.DumpPrivKeyAsync(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result.AsString(), result);
        }

        [TestMethod]
        public async Task TestGetNewAddress()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetNewAddressAsync).ToLower());
            var result = await rpc.GetNewAddressAsync();
            Assert.AreEqual(test.Response.Result.AsString(), result);
        }

        [TestMethod]
        public async Task TestGetWalletBalance()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetWalletBalanceAsync).ToLower());
            var result = await rpc.GetWalletBalanceAsync(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result["balance"].AsString(), result.Value.ToString());
        }

        [TestMethod]
        public async Task TestGetWalletUnclaimedGas()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetWalletUnclaimedGasAsync).ToLower());
            var result = await rpc.GetWalletUnclaimedGasAsync();
            Assert.AreEqual(test.Response.Result.AsString(), result.ToString());
        }

        [TestMethod]
        public async Task TestImportPrivKey()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.ImportPrivKeyAsync).ToLower());
            var result = await rpc.ImportPrivKeyAsync(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        [TestMethod]
        public async Task TestListAddress()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.ListAddressAsync).ToLower());
            var result = await rpc.ListAddressAsync();
            Assert.AreEqual(test.Response.Result.ToString(), ((JArray)result.Select(p => p.ToJson()).ToArray()).ToString());
        }

        [TestMethod]
        public async Task TestOpenWallet()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.OpenWalletAsync).ToLower());
            var result = await rpc.OpenWalletAsync(test.Request.Params[0].AsString(), test.Request.Params[1].AsString());
            Assert.AreEqual(test.Response.Result.AsBoolean(), result);
        }

        [TestMethod]
        public async Task TestSendFrom()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.SendFromAsync).ToLower());
            var result = await rpc.SendFromAsync(test.Request.Params[0].AsString(), test.Request.Params[1].AsString(),
                test.Request.Params[2].AsString(), test.Request.Params[3].AsString());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToString());
        }

        [TestMethod]
        public async Task TestSendMany()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.SendManyAsync).ToLower());
            var result = await rpc.SendManyAsync(test.Request.Params[0].AsString(), ((JArray)test.Request.Params[1]).Select(p => RpcTransferOut.FromJson(p)));
            Assert.AreEqual(test.Response.Result.ToString(), result.ToString());
        }

        [TestMethod]
        public async Task TestSendToAddress()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.SendToAddressAsync).ToLower());
            var result = await rpc.SendToAddressAsync(test.Request.Params[0].AsString(), test.Request.Params[1].AsString(), test.Request.Params[2].AsString());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToString());
        }

        #endregion Wallet

        #region Plugins

        [TestMethod()]
        public async Task GetApplicationLogTest()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetApplicationLogAsync).ToLower());
            var result = await rpc.GetApplicationLogAsync(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        [TestMethod()]
        public async Task GetNep5TransfersTest()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetNep5TransfersAsync).ToLower());
            var result = await rpc.GetNep5TransfersAsync(test.Request.Params[0].AsString(), (ulong)test.Request.Params[1].AsNumber(), (ulong)test.Request.Params[2].AsNumber());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
            test = TestUtils.RpcTestCases.Find(p => p.Name == (nameof(rpc.GetNep5TransfersAsync).ToLower() + "_with_null_transferaddress"));
            result = await rpc.GetNep5TransfersAsync(test.Request.Params[0].AsString(), (ulong)test.Request.Params[1].AsNumber(), (ulong)test.Request.Params[2].AsNumber());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        [TestMethod()]
        public async Task GetNep5BalancesTest()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(rpc.GetNep5BalancesAsync).ToLower());
            var result = await rpc.GetNep5BalancesAsync(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        #endregion Plugins
    }
}
