using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;

namespace Neo.Network.RPC.Tests
{
    [TestClass]
    public class UT_ContractClient
    {
        Mock<RpcClient> rpcClientMock;
        KeyPair keyPair1;
        UInt160 sender;

        [TestInitialize]
        public void TestSetup()
        {
            keyPair1 = new KeyPair(Wallet.GetPrivateKeyFromWIF("KyXwTh1hB76RRMquSvnxZrJzQx7h9nQP2PCRL38v6VDb5ip3nf1p"));
            sender = Contract.CreateSignatureRedeemScript(keyPair1.PublicKey).ToScriptHash();
            rpcClientMock = UT_TransactionManager.MockRpcClient(sender, new byte[0]);
        }

        [TestMethod]
        public void TestInvoke()
        {
            byte[] testScript = NativeContract.GAS.Hash.MakeScript("balanceOf", UInt160.Zero);
            UT_TransactionManager.MockInvokeScript(rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.ByteArray, Value = "00e057eb481b".HexToBytes() });

            ContractClient contractClient = new ContractClient(rpcClientMock.Object);
            var result = contractClient.TestInvoke(NativeContract.GAS.Hash, "balanceOf", UInt160.Zero);

            Assert.AreEqual(30000000000000L, (long)result.Stack[0].ToStackItem().GetBigInteger());
        }

        [TestMethod]
        public void TestDeployContract()
        {
            byte[] script;
            var manifest = ContractManifest.CreateDefault(new byte[1].ToScriptHash());
            manifest.Features = ContractFeatures.HasStorage | ContractFeatures.Payable;
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitSysCall(InteropService.Contract.Create, new byte[1], manifest.ToString());
                script = sb.ToArray();
            }

            UT_TransactionManager.MockInvokeScript(rpcClientMock, script, new ContractParameter());

            ContractClient contractClient = new ContractClient(rpcClientMock.Object);
            var result = contractClient.CreateDeployContractTx(new byte[1], manifest, keyPair1);

            Assert.IsNotNull(result);
        }
    }
}
