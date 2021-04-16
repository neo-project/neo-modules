using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Network.RPC;
using Neo.FileStorage.InnerRing.Invoker;
using Neo.SmartContract.Native;
using Neo.Wallets;
using Moq;
using Neo.Network.RPC.Models;
using Neo.IO.Json;

namespace Neo.FileStorage.Tests.InnerRing.Invoker
{
    [TestClass]
    public class UT_MainClient
    {
        private NeoSystem system;
        private Wallet wallet;

        [TestInitialize]
        public void TestSetup()
        {
            system = TestBlockchain.TheNeoSystem;
            wallet = new MyWallet("test");
            wallet.CreateAccount("2931fe84623e29817503fd9529bb10472cbb02b4e2de404a8edbfdc669262e16".HexToBytes());
        }

        public static void MockInvokeScript(Mock<RpcClient> mockClient, RpcRequest request, RpcInvokeResult result)
        {
            mockClient.Setup(p => p.RpcSendAsync("invokescript", It.Is<JObject[]>(j => j.ToString() == request.Params.ToString())))
                .ReturnsAsync(result.ToJson())
                .Verifiable();
        }

        [TestMethod]
        public void InvokeFunctionTest()
        {
            MainClient client = new MainClient(new string[] { "http://seed1t.neo.org:20332" }, wallet);
            var mockRpc = new Mock<RpcClient>(MockBehavior.Strict, "http://seed1t.neo.org:20332", null, null);
            // MockInvokeScript
            var test = TestUtils.RpcTestCases.Find(p => p.Name == "InvokeLocalScriptAsync");
            var request = test.Request;
            var response = test.Response;
            MockInvokeScript(mockRpc, request, RpcInvokeResult.FromJson(response.Result));
            // MockHeight
            mockRpc.Setup(p => p.RpcSendAsync("getblockcount")).ReturnsAsync(100).Verifiable();
            // MockCalculateNetworkfee
            var networkfee = new JObject();
            networkfee["networkfee"] = 100000000;
            mockRpc.Setup(p => p.RpcSendAsync("calculatenetworkfee", It.Is<JObject[]>(u => true)))
                .ReturnsAsync(networkfee)
                .Verifiable();
            // MockCalculatenetworkfee
            mockRpc.Setup(p => p.RpcSendAsync("sendrawtransaction", It.Is<JObject[]>(u => true)))
                .ReturnsAsync(true)
                .Verifiable();
            client.clients = new RpcClient[] { mockRpc.Object };
            bool result = client.Invoke(out _,NativeContract.GAS.Hash, "balanceOf", (long)100, UInt160.Zero);
            Assert.AreEqual(result, true);
        }
    }
}
