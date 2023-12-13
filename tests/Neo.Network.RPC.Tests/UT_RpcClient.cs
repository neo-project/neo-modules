using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using Neo.IO;
using Neo.Json;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC.Models;
using Neo.SmartContract;
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
        RpcClient _rpc;
        Mock<HttpMessageHandler> _handlerMock;

        [TestInitialize]
        public void TestSetup()
        {
            _handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);

            // use real http client with mocked handler here
            var httpClient = new HttpClient(_handlerMock.Object);
            _rpc = new RpcClient(httpClient, new Uri("http://seed1.neo.org:10331"), null);
            foreach (var test in TestUtils.RpcTestCases)
            {
                MockResponse(test.Request, test.Response);
            }
        }

        private void MockResponse(RpcRequest request, RpcResponse response)
        {
            _handlerMock.Protected()
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
            var test = TestUtils.RpcTestCases.Find(p => p.Name == (nameof(_rpc.SendRawTransactionAsync) + "error").ToLower());
            try
            {
                var result = await _rpc.SendRawTransactionAsync(Convert.FromBase64String(test.Request.Params[0].AsString()).AsSerializable<Transaction>());
            }
            catch (RpcException ex)
            {
                Assert.AreEqual(-500, ex.HResult);
                Assert.AreEqual("InsufficientFunds", ex.Message);
            }
        }

        [TestMethod]
        public async Task TestNoThrowErrorResponse()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == (nameof(_rpc.SendRawTransactionAsync) + "error").ToLower());
            _handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            _handlerMock.Protected()
               // Setup the PROTECTED method to mock
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.IsAny<HttpRequestMessage>(),
                  ItExpr.IsAny<CancellationToken>())
               // prepare the expected response of the mocked http call
               .ReturnsAsync(new HttpResponseMessage()
               {
                   StatusCode = HttpStatusCode.OK,
                   Content = new StringContent(test.Response.ToJson().ToString()),
               })
               .Verifiable();

            var httpClient = new HttpClient(_handlerMock.Object);
            var client = new RpcClient(httpClient, new Uri("http://seed1.neo.org:10331"), null);
            var response = await client.SendAsync(test.Request, false);

            Assert.IsNull(response.Result);
            Assert.IsNotNull(response.Error);
            Assert.AreEqual(-500, response.Error.Code);
            Assert.AreEqual("InsufficientFunds", response.Error.Message);
        }

        [TestMethod]
        public void TestConstructorByUrlAndDispose()
        {
            //dummy url for test
            var client = new RpcClient(new Uri("http://www.xxx.yyy"));
            Action action = () => client.Dispose();
            action.Should().NotThrow<Exception>();
        }

        [TestMethod]
        public void TestConstructorWithBasicAuth()
        {
            var client = new RpcClient(new Uri("http://www.xxx.yyy"), "krain", "123456");
            client.Dispose();
        }

        #region Blockchain

        [TestMethod]
        public async Task TestGetBestBlockHash()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(_rpc.GetBestBlockHashAsync).ToLower());
            var result = await _rpc.GetBestBlockHashAsync();
            Assert.AreEqual(test.Response.Result.AsString(), result);
        }

        [TestMethod]
        public async Task TestGetBlockHex()
        {
            var tests = TestUtils.RpcTestCases.Where(p => p.Name == nameof(_rpc.GetBlockHexAsync).ToLower());
            foreach (var test in tests)
            {
                var result = await _rpc.GetBlockHexAsync(test.Request.Params[0].AsString());
                Assert.AreEqual(test.Response.Result.AsString(), result);
            }
        }

        [TestMethod]
        public async Task TestGetBlock()
        {
            var tests = TestUtils.RpcTestCases.Where(p => p.Name == nameof(_rpc.GetBlockAsync).ToLower());
            foreach (var test in tests)
            {
                var result = await _rpc.GetBlockAsync(test.Request.Params[0].AsString());
                Assert.AreEqual(test.Response.Result.AsString(), result.ToJson(_rpc.ProtocolSettings).ToString());
            }
        }

        [TestMethod]
        public async Task TestGetBlockHeaderCount()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(_rpc.GetBlockHeaderCountAsync).ToLower());
            var result = await _rpc.GetBlockHeaderCountAsync();
            Assert.AreEqual(test.Response.Result.AsString(), result.ToString());
        }

        [TestMethod]
        public async Task TestGetBlockCount()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(_rpc.GetBlockCountAsync).ToLower());
            var result = await _rpc.GetBlockCountAsync();
            Assert.AreEqual(test.Response.Result.AsString(), result.ToString());
        }

        [TestMethod]
        public async Task TestGetBlockHash()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(_rpc.GetBlockHashAsync).ToLower());
            var result = await _rpc.GetBlockHashAsync((uint)test.Request.Params[0].AsNumber());
            Assert.AreEqual(test.Response.Result.AsString(), result.ToString());
        }

        [TestMethod]
        public async Task TestGetBlockHeaderHex()
        {
            var tests = TestUtils.RpcTestCases.Where(p => p.Name == nameof(_rpc.GetBlockHeaderHexAsync).ToLower());
            foreach (var test in tests)
            {
                var result = await _rpc.GetBlockHeaderHexAsync(test.Request.Params[0].AsString());
                Assert.AreEqual(test.Response.Result.AsString(), result);
            }
        }

        [TestMethod]
        public async Task TestGetBlockHeader()
        {
            var tests = TestUtils.RpcTestCases.Where(p => p.Name == nameof(_rpc.GetBlockHeaderAsync).ToLower());
            foreach (var test in tests)
            {
                var result = await _rpc.GetBlockHeaderAsync(test.Request.Params[0].AsString());
                Assert.AreEqual(test.Response.Result.ToString(), result.ToJson(_rpc.ProtocolSettings).ToString());
            }
        }

        [TestMethod]
        public async Task TestGetCommittee()
        {
            var tests = TestUtils.RpcTestCases.Where(p => p.Name == nameof(_rpc.GetCommitteeAsync).ToLower());
            foreach (var test in tests)
            {
                var result = await _rpc.GetCommitteeAsync();
                Assert.AreEqual(test.Response.Result.ToString(), ((JArray)result.Select(p => (JToken)p).ToArray()).ToString());
            }
        }

        [TestMethod]
        public async Task TestGetContractState()
        {
            var tests = TestUtils.RpcTestCases.Where(p => p.Name == nameof(_rpc.GetContractStateAsync).ToLower());
            foreach (var test in tests)
            {
                var result = await _rpc.GetContractStateAsync(test.Request.Params[0].AsString());
                Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
            }
        }

        [TestMethod]
        public async Task TestGetNativeContracts()
        {
            var tests = TestUtils.RpcTestCases.Where(p => p.Name == nameof(_rpc.GetNativeContractsAsync).ToLower());
            foreach (var test in tests)
            {
                var result = await _rpc.GetNativeContractsAsync();
                Assert.AreEqual(test.Response.Result.ToString(), ((JArray)result.Select(p => p.ToJson()).ToArray()).ToString());
            }
        }

        [TestMethod]
        public async Task TestGetRawMempool()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(_rpc.GetRawMempoolAsync).ToLower());
            var result = await _rpc.GetRawMempoolAsync();
            Assert.AreEqual(test.Response.Result.ToString(), ((JArray)result.Select(p => (JToken)p).ToArray()).ToString());
        }

        [TestMethod]
        public async Task TestGetRawMempoolBoth()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(_rpc.GetRawMempoolBothAsync).ToLower());
            var result = await _rpc.GetRawMempoolBothAsync();
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        [TestMethod]
        public async Task TestGetRawTransactionHex()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(_rpc.GetRawTransactionHexAsync).ToLower());
            var result = await _rpc.GetRawTransactionHexAsync(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result.AsString(), result);
        }

        [TestMethod]
        public async Task TestGetRawTransaction()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(_rpc.GetRawTransactionAsync).ToLower());
            var result = await _rpc.GetRawTransactionAsync(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson(_rpc.ProtocolSettings).ToString());
        }

        [TestMethod]
        public async Task TestGetStorage()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(_rpc.GetStorageAsync).ToLower());
            var result = await _rpc.GetStorageAsync(test.Request.Params[0].AsString(), test.Request.Params[1].AsString());
            Assert.AreEqual(test.Response.Result.AsString(), result);
        }

        [TestMethod]
        public async Task TestGetTransactionHeight()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(_rpc.GetTransactionHeightAsync).ToLower());
            var result = await _rpc.GetTransactionHeightAsync(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToString());
        }

        [TestMethod]
        public async Task TestGetNextBlockValidators()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(_rpc.GetNextBlockValidatorsAsync).ToLower());
            var result = await _rpc.GetNextBlockValidatorsAsync();
            Assert.AreEqual(test.Response.Result.ToString(), ((JArray)result.Select(p => p.ToJson()).ToArray()).ToString());
        }

        #endregion Blockchain

        #region Node

        [TestMethod]
        public async Task TestGetConnectionCount()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(_rpc.GetConnectionCountAsync).ToLower());
            var result = await _rpc.GetConnectionCountAsync();
            Assert.AreEqual(test.Response.Result.ToString(), result.ToString());
        }

        [TestMethod]
        public async Task TestGetPeers()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(_rpc.GetPeersAsync).ToLower());
            var result = await _rpc.GetPeersAsync();
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        [TestMethod]
        public async Task TestGetVersion()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(_rpc.GetVersionAsync).ToLower());
            var result = await _rpc.GetVersionAsync();
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        [TestMethod]
        public async Task TestSendRawTransaction()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(_rpc.SendRawTransactionAsync).ToLower());
            var result = await _rpc.SendRawTransactionAsync(Convert.FromBase64String(test.Request.Params[0].AsString()).AsSerializable<Transaction>());
            Assert.AreEqual(test.Response.Result["hash"].AsString(), result.ToString());
        }

        [TestMethod]
        public async Task TestSubmitBlock()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(_rpc.SubmitBlockAsync).ToLower());
            var result = await _rpc.SubmitBlockAsync(Convert.FromBase64String(test.Request.Params[0].AsString()));
            Assert.AreEqual(test.Response.Result["hash"].AsString(), result.ToString());
        }

        #endregion Node

        #region SmartContract

        [TestMethod]
        public async Task TestInvokeFunction()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(_rpc.InvokeFunctionAsync).ToLower());
            var result = await _rpc.InvokeFunctionAsync(test.Request.Params[0].AsString(), test.Request.Params[1].AsString(),
                ((JArray)test.Request.Params[2]).Select(p => RpcStack.FromJson((JObject)p)).ToArray());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());

            // TODO test verify method
        }

        [TestMethod]
        public async Task TestInvokeScript()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(_rpc.InvokeScriptAsync).ToLower());
            var result = await _rpc.InvokeScriptAsync(Convert.FromBase64String(test.Request.Params[0].AsString()));
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        [TestMethod]
        public async Task TestGetUnclaimedGas()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(_rpc.GetUnclaimedGasAsync).ToLower());
            var result = await _rpc.GetUnclaimedGasAsync(test.Request.Params[0].AsString());
            Assert.AreEqual(result.ToJson().AsString(), RpcUnclaimedGas.FromJson(result.ToJson()).ToJson().AsString());
            Assert.AreEqual(test.Response.Result["unclaimed"].AsString(), result.Unclaimed.ToString());
        }

        #endregion SmartContract

        #region Utilities

        [TestMethod]
        public async Task TestListPlugins()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(_rpc.ListPluginsAsync).ToLower());
            var result = await _rpc.ListPluginsAsync();
            Assert.AreEqual(test.Response.Result.ToString(), ((JArray)result.Select(p => p.ToJson()).ToArray()).ToString());
        }

        [TestMethod]
        public async Task TestValidateAddress()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(_rpc.ValidateAddressAsync).ToLower());
            var result = await _rpc.ValidateAddressAsync(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        #endregion Utilities

        #region Wallet

        [TestMethod]
        public async Task TestCloseWallet()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(_rpc.CloseWalletAsync).ToLower());
            var result = await _rpc.CloseWalletAsync();
            Assert.AreEqual(test.Response.Result.AsBoolean(), result);
        }

        [TestMethod]
        public async Task TestDumpPrivKey()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(_rpc.DumpPrivKeyAsync).ToLower());
            var result = await _rpc.DumpPrivKeyAsync(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result.AsString(), result);
        }

        [TestMethod]
        public async Task TestGetNewAddress()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(_rpc.GetNewAddressAsync).ToLower());
            var result = await _rpc.GetNewAddressAsync();
            Assert.AreEqual(test.Response.Result.AsString(), result);
        }

        [TestMethod]
        public async Task TestGetWalletBalance()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(_rpc.GetWalletBalanceAsync).ToLower());
            var result = await _rpc.GetWalletBalanceAsync(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result["balance"].AsString(), result.Value.ToString());
        }

        [TestMethod]
        public async Task TestGetWalletUnclaimedGas()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(_rpc.GetWalletUnclaimedGasAsync).ToLower());
            var result = await _rpc.GetWalletUnclaimedGasAsync();
            Assert.AreEqual(test.Response.Result.AsString(), result.ToString());
        }

        [TestMethod]
        public async Task TestImportPrivKey()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(_rpc.ImportPrivKeyAsync).ToLower());
            var result = await _rpc.ImportPrivKeyAsync(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        [TestMethod]
        public async Task TestListAddress()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(_rpc.ListAddressAsync).ToLower());
            var result = await _rpc.ListAddressAsync();
            Assert.AreEqual(test.Response.Result.ToString(), ((JArray)result.Select(p => p.ToJson()).ToArray()).ToString());
        }

        [TestMethod]
        public async Task TestOpenWallet()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(_rpc.OpenWalletAsync).ToLower());
            var result = await _rpc.OpenWalletAsync(test.Request.Params[0].AsString(), test.Request.Params[1].AsString());
            Assert.AreEqual(test.Response.Result.AsBoolean(), result);
        }

        [TestMethod]
        public async Task TestSendFrom()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(_rpc.SendFromAsync).ToLower());
            var result = await _rpc.SendFromAsync(test.Request.Params[0].AsString(), test.Request.Params[1].AsString(),
                test.Request.Params[2].AsString(), test.Request.Params[3].AsString());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToString());
        }

        [TestMethod]
        public async Task TestSendMany()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(_rpc.SendManyAsync).ToLower());
            var result = await _rpc.SendManyAsync(test.Request.Params[0].AsString(), ((JArray)test.Request.Params[1]).Select(p => RpcTransferOut.FromJson((JObject)p, _rpc.ProtocolSettings)));
            Assert.AreEqual(test.Response.Result.ToString(), result.ToString());
        }

        [TestMethod]
        public async Task TestSendToAddress()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(_rpc.SendToAddressAsync).ToLower());
            var result = await _rpc.SendToAddressAsync(test.Request.Params[0].AsString(), test.Request.Params[1].AsString(), test.Request.Params[2].AsString());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToString());
        }

        #endregion Wallet

        #region Plugins

        [TestMethod()]
        public async Task GetApplicationLogTest()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(_rpc.GetApplicationLogAsync).ToLower());
            var result = await _rpc.GetApplicationLogAsync(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        [TestMethod()]
        public async Task GetApplicationLogTest_TriggerType()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == (nameof(_rpc.GetApplicationLogAsync) + "_triggertype").ToLower());
            var result = await _rpc.GetApplicationLogAsync(test.Request.Params[0].AsString(), TriggerType.OnPersist);
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson().ToString());
        }

        [TestMethod()]
        public async Task GetNep17TransfersTest()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(_rpc.GetNep17TransfersAsync).ToLower());
            var result = await _rpc.GetNep17TransfersAsync(test.Request.Params[0].AsString(), (ulong)test.Request.Params[1].AsNumber(), (ulong)test.Request.Params[2].AsNumber());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson(_rpc.ProtocolSettings).ToString());
            test = TestUtils.RpcTestCases.Find(p => p.Name == (nameof(_rpc.GetNep17TransfersAsync).ToLower() + "_with_null_transferaddress"));
            result = await _rpc.GetNep17TransfersAsync(test.Request.Params[0].AsString(), (ulong)test.Request.Params[1].AsNumber(), (ulong)test.Request.Params[2].AsNumber());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson(_rpc.ProtocolSettings).ToString());
        }

        [TestMethod()]
        public async Task GetNep17BalancesTest()
        {
            var test = TestUtils.RpcTestCases.Find(p => p.Name == nameof(_rpc.GetNep17BalancesAsync).ToLower());
            var result = await _rpc.GetNep17BalancesAsync(test.Request.Params[0].AsString());
            Assert.AreEqual(test.Response.Result.ToString(), result.ToJson(_rpc.ProtocolSettings).ToString());
        }

        #endregion Plugins
    }
}
