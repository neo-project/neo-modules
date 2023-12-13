using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using System.Numerics;
using System.Threading.Tasks;

namespace Neo.Network.RPC.Tests
{
    [TestClass]
    public class UT_PolicyAPI
    {
        private Mock<RpcClient> _rpcClientMock;
        private KeyPair _keyPair1;
        private UInt160 _sender;
        private PolicyAPI _policyApi;

        [TestInitialize]
        public void TestSetup()
        {
            _keyPair1 = new KeyPair(Wallet.GetPrivateKeyFromWIF("KyXwTh1hB76RRMquSvnxZrJzQx7h9nQP2PCRL38v6VDb5ip3nf1p"));
            _sender = Contract.CreateSignatureRedeemScript(_keyPair1.PublicKey).ToScriptHash();
            _rpcClientMock = UT_TransactionManager.MockRpcClient(_sender, Array.Empty<byte>());
            _policyApi = new PolicyAPI(_rpcClientMock.Object);
        }

        [TestMethod]
        public async Task TestGetExecFeeFactor()
        {
            byte[] testScript = NativeContract.Policy.Hash.MakeScript("getExecFeeFactor");
            UT_TransactionManager.MockInvokeScript(_rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(30) });

            var result = await _policyApi.GetExecFeeFactorAsync();
            Assert.AreEqual(30u, result);
        }

        [TestMethod]
        public async Task TestGetStoragePrice()
        {
            byte[] testScript = NativeContract.Policy.Hash.MakeScript("getStoragePrice");
            UT_TransactionManager.MockInvokeScript(_rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(100000) });

            var result = await _policyApi.GetStoragePriceAsync();
            Assert.AreEqual(100000u, result);
        }

        [TestMethod]
        public async Task TestGetFeePerByte()
        {
            byte[] testScript = NativeContract.Policy.Hash.MakeScript("getFeePerByte");
            UT_TransactionManager.MockInvokeScript(_rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(1000) });

            var result = await _policyApi.GetFeePerByteAsync();
            Assert.AreEqual(1000L, result);
        }

        [TestMethod]
        public async Task TestIsBlocked()
        {
            byte[] testScript = NativeContract.Policy.Hash.MakeScript("isBlocked", UInt160.Zero);
            UT_TransactionManager.MockInvokeScript(_rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.Boolean, Value = true });
            var result = await _policyApi.IsBlockedAsync(UInt160.Zero);
            Assert.AreEqual(true, result);
        }
    }
}
