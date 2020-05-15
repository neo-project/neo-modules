using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.IO.Json;
using Neo.Network.RPC.Models;
using System.Linq;

namespace Neo.Network.RPC.Tests
{
    [TestClass()]
    public class UT_RpcModels
    {
        [TestMethod()]
        public void TestRpcAccount()
        {
            JObject json = TestUtils.RpcTestCases.Find(p => p.Name == nameof(RpcClient.ImportPrivKey).ToLower()).Response.Result;
            var item = RpcAccount.FromJson(json);
            Assert.AreEqual(json.ToString(), item.ToJson().ToString());
        }

        [TestMethod()]
        public void TestRpcApplicationLog()
        {
            JObject json = TestUtils.RpcTestCases.Find(p => p.Name == nameof(RpcClient.GetApplicationLog).ToLower()).Response.Result;
            var item = RpcApplicationLog.FromJson(json);
            Assert.AreEqual(json.ToString(), item.ToJson().ToString());
        }

        [TestMethod()]
        public void TestRpcBlock()
        {
            JObject json = TestUtils.RpcTestCases.Find(p => p.Name == nameof(RpcClient.GetBlock).ToLower()).Response.Result;
            var item = RpcBlock.FromJson(json);
            Assert.AreEqual(json.ToString(), item.ToJson().ToString());
        }

        [TestMethod()]
        public void TestRpcBlockHeader()
        {
            JObject json = TestUtils.RpcTestCases.Find(p => p.Name == nameof(RpcClient.GetBlockHeader).ToLower()).Response.Result;
            var item = RpcBlockHeader.FromJson(json);
            Assert.AreEqual(json.ToString(), item.ToJson().ToString());
        }

        [TestMethod()]
        public void TestGetContractState()
        {
            JObject json = TestUtils.RpcTestCases.Find(p => p.Name == nameof(RpcClient.GetContractState).ToLower()).Response.Result;
            var item = RpcContractState.FromJson(json);
            Assert.AreEqual(json.ToString(), item.ToJson().ToString());
        }

        [TestMethod()]
        public void TestRpcInvokeResult()
        {
            JObject json = TestUtils.RpcTestCases.Find(p => p.Name == nameof(RpcClient.InvokeFunction).ToLower()).Response.Result;
            var item = RpcInvokeResult.FromJson(json);
            Assert.AreEqual(json.ToString(), item.ToJson().ToString());
        }

        [TestMethod()]
        public void TestRpcNep5Balances()
        {
            JObject json = TestUtils.RpcTestCases.Find(p => p.Name == nameof(RpcClient.GetNep5Balances).ToLower()).Response.Result;
            var item = RpcNep5Balances.FromJson(json);
            Assert.AreEqual(json.ToString(), item.ToJson().ToString());
        }

        [TestMethod()]
        public void TestRpcNep5Transfers()
        {
            JObject json = TestUtils.RpcTestCases.Find(p => p.Name == nameof(RpcClient.GetNep5Transfers).ToLower()).Response.Result;
            var item = RpcNep5Transfers.FromJson(json);
            Assert.AreEqual(json.ToString(), item.ToJson().ToString());
        }

        [TestMethod()]
        public void TestRpcPeers()
        {
            JObject json = TestUtils.RpcTestCases.Find(p => p.Name == nameof(RpcClient.GetPeers).ToLower()).Response.Result;
            var item = RpcPeers.FromJson(json);
            Assert.AreEqual(json.ToString(), item.ToJson().ToString());
        }

        [TestMethod()]
        public void TestRpcPlugin()
        {
            JObject json = TestUtils.RpcTestCases.Find(p => p.Name == nameof(RpcClient.ListPlugins).ToLower()).Response.Result;
            var item = ((JArray)json).Select(p => RpcPlugin.FromJson(p));
            Assert.AreEqual(json.ToString(), ((JArray)item.Select(p => p.ToJson()).ToArray()).ToString());
        }

        [TestMethod()]
        public void TestRpcRawMemPool()
        {
            JObject json = TestUtils.RpcTestCases.Find(p => p.Name == nameof(RpcClient.GetRawMempoolBoth).ToLower()).Response.Result;
            var item = RpcRawMemPool.FromJson(json);
            Assert.AreEqual(json.ToString(), item.ToJson().ToString());
        }

        [TestMethod()]
        public void TestRpcTransaction()
        {
            JObject json = TestUtils.RpcTestCases.Find(p => p.Name == nameof(RpcClient.GetRawTransaction).ToLower()).Response.Result;
            var item = RpcTransaction.FromJson(json);
            Assert.AreEqual(json.ToString(), item.ToJson().ToString());
        }

        [TestMethod()]
        public void TestRpcTransferOut()
        {
            JObject json = TestUtils.RpcTestCases.Find(p => p.Name == nameof(RpcClient.SendMany).ToLower()).Request.Params[1];
            var item = ((JArray)json).Select(p => RpcTransferOut.FromJson(p));
            Assert.AreEqual(json.ToString(), ((JArray)item.Select(p => p.ToJson()).ToArray()).ToString());
        }

        [TestMethod()]
        public void TestRpcValidateAddressResult()
        {
            JObject json = TestUtils.RpcTestCases.Find(p => p.Name == nameof(RpcClient.ValidateAddress).ToLower()).Response.Result;
            var item = RpcValidateAddressResult.FromJson(json);
            Assert.AreEqual(json.ToString(), item.ToJson().ToString());
        }

        [TestMethod()]
        public void TestRpcValidator()
        {
            JObject json = TestUtils.RpcTestCases.Find(p => p.Name == nameof(RpcClient.GetValidators).ToLower()).Response.Result;
            var item = ((JArray)json).Select(p => RpcValidator.FromJson(p));
            Assert.AreEqual(json.ToString(), ((JArray)item.Select(p => p.ToJson()).ToArray()).ToString());
        }

        [TestMethod()]
        public void TestRpcVersion()
        {
            JObject json = TestUtils.RpcTestCases.Find(p => p.Name == nameof(RpcClient.GetVersion).ToLower()).Response.Result;
            var item = RpcVersion.FromJson(json);
            Assert.AreEqual(json.ToString(), item.ToJson().ToString());
        }
    }
}
