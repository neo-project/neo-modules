using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
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
    public class UT_Nep5API
    {
        Mock<RpcClient> rpcClientMock;
        KeyPair keyPair1;
        UInt160 sender;
        Nep5API nep5API;

        [TestInitialize]
        public void TestSetup()
        {
            keyPair1 = new KeyPair(Wallet.GetPrivateKeyFromWIF("KyXwTh1hB76RRMquSvnxZrJzQx7h9nQP2PCRL38v6VDb5ip3nf1p"));
            sender = Contract.CreateSignatureRedeemScript(keyPair1.PublicKey).ToScriptHash();
            rpcClientMock = UT_TransactionManager.MockRpcClient(sender, new byte[0]);
            nep5API = new Nep5API(rpcClientMock.Object);
        }

        [TestMethod]
        public async Task TestBalanceOf()
        {
            byte[] testScript = NativeContract.GAS.Hash.MakeScript("balanceOf", UInt160.Zero);
            UT_TransactionManager.MockInvokeScript(rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(10000) });

            var balance = await nep5API.BalanceOfAsync(NativeContract.GAS.Hash, UInt160.Zero);
            Assert.AreEqual(10000, (int)balance);
        }

        [TestMethod]
        public async Task TestGetName()
        {
            byte[] testScript = NativeContract.GAS.Hash.MakeScript("name");
            UT_TransactionManager.MockInvokeScript(rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.String, Value = NativeContract.GAS.Name });

            var result = await nep5API.NameAsync(NativeContract.GAS.Hash);
            Assert.AreEqual(NativeContract.GAS.Name, result);
        }

        [TestMethod]
        public async Task TestGetSymbol()
        {
            byte[] testScript = NativeContract.GAS.Hash.MakeScript("symbol");
            UT_TransactionManager.MockInvokeScript(rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.String, Value = NativeContract.GAS.Symbol });

            var result = await nep5API.SymbolAsync(NativeContract.GAS.Hash);
            Assert.AreEqual(NativeContract.GAS.Symbol, result);
        }

        [TestMethod]
        public async Task TestGetDecimals()
        {
            byte[] testScript = NativeContract.GAS.Hash.MakeScript("decimals");
            UT_TransactionManager.MockInvokeScript(rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(NativeContract.GAS.Decimals) });

            var result = await nep5API.DecimalsAsync(NativeContract.GAS.Hash);
            Assert.AreEqual(NativeContract.GAS.Decimals, result);
        }

        [TestMethod]
        public async Task TestGetTotalSupply()
        {
            byte[] testScript = NativeContract.GAS.Hash.MakeScript("totalSupply");
            UT_TransactionManager.MockInvokeScript(rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(1_00000000) });

            var result = await nep5API.TotalSupplyAsync(NativeContract.GAS.Hash);
            Assert.AreEqual(1_00000000, (int)result);
        }

        [TestMethod]
        public async Task TestGetTokenInfo()
        {
            UInt160 scriptHash = NativeContract.GAS.Hash;
            byte[] testScript = scriptHash.MakeScript("name")
                .Concat(scriptHash.MakeScript("symbol"))
                .Concat(scriptHash.MakeScript("decimals"))
                .Concat(scriptHash.MakeScript("totalSupply"))
                .ToArray(); ;
            UT_TransactionManager.MockInvokeScript(rpcClientMock, testScript,
                new ContractParameter { Type = ContractParameterType.String, Value = NativeContract.GAS.Name },
                new ContractParameter { Type = ContractParameterType.String, Value = NativeContract.GAS.Symbol },
                new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(NativeContract.GAS.Decimals) },
                new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(1_00000000) });

            var result = await nep5API.GetTokenInfoAsync(NativeContract.GAS.Hash);
            Assert.AreEqual(NativeContract.GAS.Name, result.Name);
            Assert.AreEqual(NativeContract.GAS.Symbol, result.Symbol);
            Assert.AreEqual(8, (int)result.Decimals);
            Assert.AreEqual(1_00000000, (int)result.TotalSupply);
        }

        [TestMethod]
        public async Task TestTransfer()
        {
            byte[] testScript = NativeContract.GAS.Hash.MakeScript("transfer", sender, UInt160.Zero, new BigInteger(1_00000000));
            UT_TransactionManager.MockInvokeScript(rpcClientMock, testScript, new ContractParameter());

            var result = await nep5API.CreateTransferTxAsync(NativeContract.GAS.Hash, keyPair1, UInt160.Zero, new BigInteger(1_00000000));
            Assert.IsNotNull(result);
        }
    }
}
