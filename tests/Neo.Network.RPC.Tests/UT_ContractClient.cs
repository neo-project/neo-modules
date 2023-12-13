using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Neo.SmartContract;
using Neo.SmartContract.Manifest;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using System.Threading.Tasks;

namespace Neo.Network.RPC.Tests
{
    [TestClass]
    public class UT_ContractClient
    {
        private Mock<RpcClient> _rpcClientMock;
        private KeyPair _keyPair1;
        private UInt160 _sender;

        [TestInitialize]
        public void TestSetup()
        {
            _keyPair1 = new KeyPair(Wallet.GetPrivateKeyFromWIF("KyXwTh1hB76RRMquSvnxZrJzQx7h9nQP2PCRL38v6VDb5ip3nf1p"));
            _sender = Contract.CreateSignatureRedeemScript(_keyPair1.PublicKey).ToScriptHash();
            _rpcClientMock = UT_TransactionManager.MockRpcClient(_sender, Array.Empty<byte>());
        }

        [TestMethod]
        public async Task TestInvoke()
        {
            byte[] testScript = NativeContract.GAS.Hash.MakeScript("balanceOf", UInt160.Zero);
            UT_TransactionManager.MockInvokeScript(_rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.ByteArray, Value = "00e057eb481b".HexToBytes() });

            ContractClient contractClient = new ContractClient(_rpcClientMock.Object);
            var result = await contractClient.TestInvokeAsync(NativeContract.GAS.Hash, "balanceOf", UInt160.Zero);

            Assert.AreEqual(30000000000000L, (long)result.Stack[0].GetInteger());
        }

        [TestMethod]
        public async Task TestDeployContract()
        {
            byte[] script;
            var manifest = new ContractManifest()
            {
                Permissions = new[] { ContractPermission.DefaultPermission },
                Abi = new ContractAbi()
                {
                    Events = Array.Empty<ContractEventDescriptor>(),
                    Methods = Array.Empty<ContractMethodDescriptor>()
                },
                Groups = Array.Empty<ContractGroup>(),
                Trusts = WildcardContainer<ContractPermissionDescriptor>.Create(),
                SupportedStandards = new string[] { "NEP-10" },
                Extra = null,
            };
            using (ScriptBuilder sb = new ScriptBuilder())
            {
                sb.EmitDynamicCall(NativeContract.ContractManagement.Hash, "deploy", new byte[1], manifest.ToJson().ToString());
                script = sb.ToArray();
            }

            UT_TransactionManager.MockInvokeScript(_rpcClientMock, script, new ContractParameter());

            ContractClient contractClient = new ContractClient(_rpcClientMock.Object);
            var result = await contractClient.CreateDeployContractTxAsync(new byte[1], manifest, _keyPair1);

            Assert.IsNotNull(result);
        }
    }
}
