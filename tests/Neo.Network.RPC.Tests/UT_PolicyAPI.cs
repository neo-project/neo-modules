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
        Mock<RpcClient> rpcClientMock;
        KeyPair keyPair1;
        UInt160 sender;
        PolicyAPI policyAPI;

        [TestInitialize]
        public void TestSetup()
        {
            keyPair1 = new KeyPair(Wallet.GetPrivateKeyFromWIF("KyXwTh1hB76RRMquSvnxZrJzQx7h9nQP2PCRL38v6VDb5ip3nf1p"));
            sender = Contract.CreateSignatureRedeemScript(keyPair1.PublicKey).ToScriptHash();
            rpcClientMock = UT_TransactionManager.MockRpcClient(sender, new byte[0]);
            policyAPI = new PolicyAPI(rpcClientMock.Object);
        }

        [TestMethod]
        public async Task TestGetMaxTransactionsPerBlock()
        {
            byte[] testScript = NativeContract.Policy.Hash.MakeScript("getMaxTransactionsPerBlock");
            UT_TransactionManager.MockInvokeScript(rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(512) });

            var result = await policyAPI.GetMaxTransactionsPerBlockAsync();
            Assert.AreEqual(512u, result);
        }

        [TestMethod]
        public async Task TestGetMaxBlockSize()
        {
            byte[] testScript = NativeContract.Policy.Hash.MakeScript("getMaxBlockSize");
            UT_TransactionManager.MockInvokeScript(rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(1024u * 256u) });

            var result = await policyAPI.GetMaxBlockSizeAsync();
            Assert.AreEqual(1024u * 256u, result);
        }

        [TestMethod]
        public async Task TestGetFeePerByte()
        {
            byte[] testScript = NativeContract.Policy.Hash.MakeScript("getFeePerByte");
            UT_TransactionManager.MockInvokeScript(rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(1000) });

            var result = await policyAPI.GetFeePerByteAsync();
            Assert.AreEqual(1000L, result);
        }

        [TestMethod]
        public async Task TestGetBlockedAccounts()
        {
            byte[] testScript = NativeContract.Policy.Hash.MakeScript("getBlockedAccounts");
            UT_TransactionManager.MockInvokeScript(rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.Array, Value = new[] { new ContractParameter { Type = ContractParameterType.Hash160, Value = UInt160.Zero } } });

            var result = await policyAPI.GetBlockedAccountsAsync();
            Assert.AreEqual(UInt160.Zero, result[0]);
        }
    }
}
