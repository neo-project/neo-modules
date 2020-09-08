using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Neo.IO.Json;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC.Models;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using System.Numerics;
using System.Threading.Tasks;

namespace Neo.Network.RPC.Tests
{
    [TestClass]
    public class UT_WalletAPI
    {
        Mock<RpcClient> rpcClientMock;
        KeyPair keyPair1;
        string address1;
        UInt160 sender;
        WalletAPI walletAPI;
        UInt160 multiSender;

        [TestInitialize]
        public void TestSetup()
        {
            keyPair1 = new KeyPair(Wallet.GetPrivateKeyFromWIF("KyXwTh1hB76RRMquSvnxZrJzQx7h9nQP2PCRL38v6VDb5ip3nf1p"));
            sender = Contract.CreateSignatureRedeemScript(keyPair1.PublicKey).ToScriptHash();
            multiSender = Contract.CreateMultiSigContract(1, keyPair1.PublicKey).ScriptHash;
            address1 = Wallets.Helper.ToAddress(sender);
            rpcClientMock = UT_TransactionManager.MockRpcClient(sender, new byte[0]);
            walletAPI = new WalletAPI(rpcClientMock.Object);
        }

        [TestMethod]
        public async Task TestGetUnclaimedGas()
        {
            byte[] testScript = NativeContract.NEO.Hash.MakeScript("unclaimedGas", sender, 99);
            UT_TransactionManager.MockInvokeScript(rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(1_10000000) });

            var balance = await walletAPI.GetUnclaimedGasAsync(address1);
            Assert.AreEqual(1.1m, balance);
        }

        [TestMethod]
        public async Task TestGetNeoBalance()
        {
            byte[] testScript = NativeContract.NEO.Hash.MakeScript("balanceOf", sender);
            UT_TransactionManager.MockInvokeScript(rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(1_00000000) });

            var balance = await walletAPI.GetNeoBalanceAsync(address1);
            Assert.AreEqual(1_00000000u, balance);
        }

        [TestMethod]
        public async Task TestGetGasBalance()
        {
            byte[] testScript = NativeContract.GAS.Hash.MakeScript("balanceOf", sender);
            UT_TransactionManager.MockInvokeScript(rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(1_10000000) });

            var balance = await walletAPI.GetGasBalanceAsync(address1);
            Assert.AreEqual(1.1m, balance);
        }

        [TestMethod]
        public async Task TestGetTokenBalance()
        {
            byte[] testScript = UInt160.Zero.MakeScript("balanceOf", sender);
            UT_TransactionManager.MockInvokeScript(rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(1_10000000) });

            var balance = await walletAPI.GetTokenBalanceAsync(UInt160.Zero.ToString(), address1);
            Assert.AreEqual(1_10000000, balance);
        }

        [TestMethod]
        public async Task TestClaimGas()
        {
            byte[] balanceScript = NativeContract.NEO.Hash.MakeScript("balanceOf", sender);
            UT_TransactionManager.MockInvokeScript(rpcClientMock, balanceScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(1_00000000) });

            byte[] testScript = NativeContract.NEO.Hash.MakeScript("transfer", sender, sender, new BigInteger(1_00000000));
            UT_TransactionManager.MockInvokeScript(rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(1_10000000) });

            var json = new JObject();
            json["hash"] = UInt256.Zero.ToString();
            rpcClientMock.Setup(p => p.RpcSendAsync("sendrawtransaction", It.IsAny<JObject>())).ReturnsAsync(json);

            var tranaction = await walletAPI.ClaimGasAsync(keyPair1.Export());
            Assert.AreEqual(testScript.ToHexString(), tranaction.Script.ToHexString());
        }

        [TestMethod]
        public async Task TestTransfer()
        {
            byte[] decimalsScript = NativeContract.GAS.Hash.MakeScript("decimals");
            UT_TransactionManager.MockInvokeScript(rpcClientMock, decimalsScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(8) });

            byte[] testScript = NativeContract.GAS.Hash.MakeScript("transfer", sender, UInt160.Zero, NativeContract.GAS.Factor * 100);
            UT_TransactionManager.MockInvokeScript(rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(1_10000000) });

            var json = new JObject();
            json["hash"] = UInt256.Zero.ToString();
            rpcClientMock.Setup(p => p.RpcSendAsync("sendrawtransaction", It.IsAny<JObject>())).ReturnsAsync(json);

            var tranaction = await walletAPI.TransferAsync(NativeContract.GAS.Hash.ToString(), keyPair1.Export(), UInt160.Zero.ToAddress(), 100);
            Assert.AreEqual(testScript.ToHexString(), tranaction.Script.ToHexString());
        }

        [TestMethod]
        public async Task TestTransferfromMultiSigAccount()
        {
            byte[] balanceScript = NativeContract.GAS.Hash.MakeScript("balanceOf", multiSender);
            var balanceResult = new ContractParameter() { Type = ContractParameterType.Integer, Value = BigInteger.Parse("10000000000000000") };

            UT_TransactionManager.MockInvokeScript(rpcClientMock, balanceScript, balanceResult);

            byte[] decimalsScript = NativeContract.GAS.Hash.MakeScript("decimals");
            UT_TransactionManager.MockInvokeScript(rpcClientMock, decimalsScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(8) });

            byte[] testScript = NativeContract.GAS.Hash.MakeScript("transfer", multiSender, UInt160.Zero, NativeContract.GAS.Factor * 100);
            UT_TransactionManager.MockInvokeScript(rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(1_10000000) });

            var json = new JObject();
            json["hash"] = UInt256.Zero.ToString();
            rpcClientMock.Setup(p => p.RpcSendAsync("sendrawtransaction", It.IsAny<JObject>())).ReturnsAsync(json);

            var tranaction = await walletAPI.TransferAsync(NativeContract.GAS.Hash, 1, new[] { keyPair1.PublicKey }, new[] { keyPair1 }, UInt160.Zero, NativeContract.GAS.Factor * 100);
            Assert.AreEqual(testScript.ToHexString(), tranaction.Script.ToHexString());
        }

        [TestMethod]
        public async Task TestWaitTransaction()
        {
            Transaction transaction = TestUtils.GetTransaction();
            rpcClientMock.Setup(p => p.RpcSendAsync("getrawtransaction", It.Is<JObject[]>(j => j[0].AsString() == transaction.Hash.ToString())))
                .ReturnsAsync(new RpcTransaction { Transaction = transaction, VMState = VMState.HALT, BlockHash = UInt256.Zero, BlockTime = 100, Confirmations = 1 }.ToJson());

            var tx = await walletAPI.WaitTransactionAsync(transaction);
            Assert.AreEqual(VMState.HALT, tx.VMState);
            Assert.AreEqual(UInt256.Zero, tx.BlockHash);
        }
    }
}
