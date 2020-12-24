using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Neo.IO.Json;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Neo.Network.RPC.Tests
{
    [TestClass]
    public class UT_Nep17API
    {
        Mock<RpcClient> rpcClientMock;
        KeyPair keyPair1;
        UInt160 sender;
        Nep17API nep17API;

        [TestInitialize]
        public void TestSetup()
        {
            keyPair1 = new KeyPair(Wallet.GetPrivateKeyFromWIF("KyXwTh1hB76RRMquSvnxZrJzQx7h9nQP2PCRL38v6VDb5ip3nf1p"));
            sender = Contract.CreateSignatureRedeemScript(keyPair1.PublicKey).ToScriptHash();
            rpcClientMock = UT_TransactionManager.MockRpcClient(sender, new byte[0]);
            nep17API = new Nep17API(rpcClientMock.Object);
        }

        [TestMethod]
        public async Task TestBalanceOf()
        {
            byte[] testScript = NativeContract.GAS.Hash.MakeScript("balanceOf", UInt160.Zero);
            UT_TransactionManager.MockInvokeScript(rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(10000) });

            var balance = await nep17API.BalanceOfAsync(NativeContract.GAS.Hash, UInt160.Zero);
            Assert.AreEqual(10000, (int)balance);
        }

        [TestMethod]
        public async Task TestGetSymbol()
        {
            byte[] testScript = NativeContract.GAS.Hash.MakeScript("symbol");
            UT_TransactionManager.MockInvokeScript(rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.String, Value = NativeContract.GAS.Symbol });

            var result = await nep17API.SymbolAsync(NativeContract.GAS.Hash);
            Assert.AreEqual(NativeContract.GAS.Symbol, result);
        }

        [TestMethod]
        public async Task TestGetDecimals()
        {
            byte[] testScript = NativeContract.GAS.Hash.MakeScript("decimals");
            UT_TransactionManager.MockInvokeScript(rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(NativeContract.GAS.Decimals) });

            var result = await nep17API.DecimalsAsync(NativeContract.GAS.Hash);
            Assert.AreEqual(NativeContract.GAS.Decimals, result);
        }

        [TestMethod]
        public async Task TestGetTotalSupply()
        {
            byte[] testScript = NativeContract.GAS.Hash.MakeScript("totalSupply");
            UT_TransactionManager.MockInvokeScript(rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(1_00000000) });

            var result = await nep17API.TotalSupplyAsync(NativeContract.GAS.Hash);
            Assert.AreEqual(1_00000000, (int)result);
        }

        [TestMethod]
        public async Task TestGetTokenInfo()
        {
            UInt160 scriptHash = NativeContract.GAS.Hash;
            byte[] testScript = scriptHash.MakeScript("symbol")
                .Concat(scriptHash.MakeScript("decimals"))
                .Concat(scriptHash.MakeScript("totalSupply"))
                .ToArray();
            UT_TransactionManager.MockInvokeScript(rpcClientMock, testScript,
                new ContractParameter { Type = ContractParameterType.String, Value = NativeContract.GAS.Symbol },
                new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(NativeContract.GAS.Decimals) },
                new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(1_00000000) });

            scriptHash = NativeContract.NEO.Hash;
            testScript = scriptHash.MakeScript("symbol")
                .Concat(scriptHash.MakeScript("decimals"))
                .Concat(scriptHash.MakeScript("totalSupply"))
                .ToArray();
            UT_TransactionManager.MockInvokeScript(rpcClientMock, testScript,
                new ContractParameter { Type = ContractParameterType.String, Value = NativeContract.NEO.Symbol },
                new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(NativeContract.NEO.Decimals) },
                new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(1_00000000) });

            var tests = TestUtils.RpcTestCases.Where(p => p.Name == "getcontractstateasync");
            foreach (var test in tests)
            {
                rpcClientMock.Setup(p => p.RpcSendAsync("getcontractstate", It.Is<JObject[]>(u => true)))
                .ReturnsAsync(test.Response.Result)
                .Verifiable();

                if (test.Request.Params[0].AsString() == "0xa6a6c15dcdc9b997dac448b6926522d22efeedfb")
                {
                    var result = await nep17API.GetTokenInfoAsync(NativeContract.GAS.Hash);
                    Assert.AreEqual(NativeContract.GAS.Symbol, result.Symbol);
                    Assert.AreEqual(8, (int)result.Decimals);
                    Assert.AreEqual(1_00000000, (int)result.TotalSupply);
                    Assert.AreEqual("GasToken", result.Name);
                }
                else if (test.Request.Params[0].AsString() == "0x0a46e2e37c9987f570b4af253fb77e7eef0f72b6")
                {
                    var result = await nep17API.GetTokenInfoAsync(NativeContract.NEO.Hash);
                    Assert.AreEqual(NativeContract.NEO.Symbol, result.Symbol);
                    Assert.AreEqual(0, (int)result.Decimals);
                    Assert.AreEqual(1_00000000, (int)result.TotalSupply);
                    Assert.AreEqual("NeoToken", result.Name);
                }
            }
        }

        [TestMethod]
        public async Task TestTransfer()
        {
            byte[] testScript = NativeContract.GAS.Hash.MakeScript("transfer", sender, UInt160.Zero, new BigInteger(1_00000000), null);
            UT_TransactionManager.MockInvokeScript(rpcClientMock, testScript, new ContractParameter());

            var result = await nep17API.CreateTransferTxAsync(NativeContract.GAS.Hash, keyPair1, UInt160.Zero, new BigInteger(1_00000000));

            testScript = NativeContract.GAS.Hash.MakeScript("transfer", sender, UInt160.Zero, new BigInteger(1_00000000), string.Empty);
            UT_TransactionManager.MockInvokeScript(rpcClientMock, testScript, new ContractParameter());

            result = await nep17API.CreateTransferTxAsync(NativeContract.GAS.Hash, keyPair1, UInt160.Zero, new BigInteger(1_00000000), string.Empty);
            Assert.IsNotNull(result);
        }
    }
}
