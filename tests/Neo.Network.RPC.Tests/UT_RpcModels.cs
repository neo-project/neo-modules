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
            JObject json = TestUtils.RpcTestCases.Find(p => p.Name == nameof(RpcClient.ImportPrivKeyAsync).ToLower()).Response.Result;
            var item = RpcAccount.FromJson(json);
            Assert.AreEqual(json.ToString(), item.ToJson().ToString());
        }

        [TestMethod()]
        public void TestRpcApplicationLog()
        {
            JObject json = TestUtils.RpcTestCases.Find(p => p.Name == nameof(RpcClient.GetApplicationLogAsync).ToLower()).Response.Result;
            var item = RpcApplicationLog.FromJson(json);
            Assert.AreEqual(json.ToString(), item.ToJson().ToString());
        }

        [TestMethod()]
        public void TestRpcBlock()
        {
            JObject json = TestUtils.RpcTestCases.Find(p => p.Name == nameof(RpcClient.GetBlockAsync).ToLower()).Response.Result;
            var item = RpcBlock.FromJson(json);
            Assert.AreEqual(json.ToString(), item.ToJson().ToString());
        }

        [TestMethod()]
        public void TestRpcBlockHeader()
        {
            JObject json = TestUtils.RpcTestCases.Find(p => p.Name == nameof(RpcClient.GetBlockHeaderAsync).ToLower()).Response.Result;
            var item = RpcBlockHeader.FromJson(json);
            Assert.AreEqual(json.ToString(), item.ToJson().ToString());
        }

        [TestMethod()]
        public void TestGetContractState()
        {
            JObject json = TestUtils.RpcTestCases.Find(p => p.Name == nameof(RpcClient.GetContractStateAsync).ToLower()).Response.Result;
            var item = RpcContractState.FromJson(json);
            Assert.AreEqual(json.ToString(), item.ToJson().ToString());
        }

        [TestMethod()]
        public void TestRpcInvokeResult()
        {
            JObject json = TestUtils.RpcTestCases.Find(p => p.Name == nameof(RpcClient.InvokeFunctionAsync).ToLower()).Response.Result;
            var item = RpcInvokeResult.FromJson(json);
            Assert.AreEqual(json.ToString(), item.ToJson().ToString());
        }

        [TestMethod()]
        public void TestRpcNep5Balances()
        {
            JObject json = TestUtils.RpcTestCases.Find(p => p.Name == nameof(RpcClient.GetNep5BalancesAsync).ToLower()).Response.Result;
            var item = RpcNep5Balances.FromJson(json);
            Assert.AreEqual(json.ToString(), item.ToJson().ToString());
        }

        [TestMethod()]
        public void TestRpcNep5Transfers()
        {
            JObject json = TestUtils.RpcTestCases.Find(p => p.Name == nameof(RpcClient.GetNep5TransfersAsync).ToLower()).Response.Result;
            var item = RpcNep5Transfers.FromJson(json);
            Assert.AreEqual(json.ToString(), item.ToJson().ToString());
        }

        [TestMethod()]
        public void TestRpcPeers()
        {
            JObject json = TestUtils.RpcTestCases.Find(p => p.Name == nameof(RpcClient.GetPeersAsync).ToLower()).Response.Result;
            var item = RpcPeers.FromJson(json);
            Assert.AreEqual(json.ToString(), item.ToJson().ToString());
        }

        [TestMethod()]
        public void TestRpcPlugin()
        {
            JObject json = TestUtils.RpcTestCases.Find(p => p.Name == nameof(RpcClient.ListPluginsAsync).ToLower()).Response.Result;
            var item = ((JArray)json).Select(p => RpcPlugin.FromJson(p));
            Assert.AreEqual(json.ToString(), ((JArray)item.Select(p => p.ToJson()).ToArray()).ToString());
        }

        [TestMethod()]
        public void TestRpcRawMemPool()
        {
            JObject json = TestUtils.RpcTestCases.Find(p => p.Name == nameof(RpcClient.GetRawMempoolBothAsync).ToLower()).Response.Result;
            var item = RpcRawMemPool.FromJson(json);
            Assert.AreEqual(json.ToString(), item.ToJson().ToString());
        }

        [TestMethod()]
        public void TestRpcTransaction()
        {
            JObject json = TestUtils.RpcTestCases.Find(p => p.Name == nameof(RpcClient.GetRawTransactionAsync).ToLower()).Response.Result;
            var item = RpcTransaction.FromJson(json);
            Assert.AreEqual(json.ToString(), item.ToJson().ToString());
        }

        [TestMethod()]
        public void TestRpcTransferOut()
        {
            JObject json = TestUtils.RpcTestCases.Find(p => p.Name == nameof(RpcClient.SendManyAsync).ToLower()).Request.Params[1];
            var item = ((JArray)json).Select(p => RpcTransferOut.FromJson(p));
            Assert.AreEqual(json.ToString(), ((JArray)item.Select(p => p.ToJson()).ToArray()).ToString());
        }

        [TestMethod()]
        public void TestRpcValidateAddressResult()
        {
            JObject json = TestUtils.RpcTestCases.Find(p => p.Name == nameof(RpcClient.ValidateAddressAsync).ToLower()).Response.Result;
            var item = RpcValidateAddressResult.FromJson(json);
            Assert.AreEqual(json.ToString(), item.ToJson().ToString());
        }

        [TestMethod()]
        public void TestRpcValidator()
        {
            JObject json = TestUtils.RpcTestCases.Find(p => p.Name == nameof(RpcClient.GetNextBlockValidatorsAsync).ToLower()).Response.Result;
            var item = ((JArray)json).Select(p => RpcValidator.FromJson(p));
            Assert.AreEqual(json.ToString(), ((JArray)item.Select(p => p.ToJson()).ToArray()).ToString());
        }

        [TestMethod()]
        public void TestRpcVersion()
        {
            JObject json = TestUtils.RpcTestCases.Find(p => p.Name == nameof(RpcClient.GetVersionAsync).ToLower()).Response.Result;
            var item = RpcVersion.FromJson(json);
            Assert.AreEqual(json.ToString(), item.ToJson().ToString());
        }
    }
}
