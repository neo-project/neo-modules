using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Neo.Cryptography.ECC;
using Neo.Json;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC.Models;
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
    public class UT_WalletAPI
    {
        Mock<RpcClient> _rpcClientMock;
        KeyPair _keyPair1;
        string _address1;
        UInt160 _sender;
        WalletAPI _walletApi;
        UInt160 _multiSender;
        RpcClient _client;

        [TestInitialize]
        public void TestSetup()
        {
            _keyPair1 = new KeyPair(Wallet.GetPrivateKeyFromWIF("KyXwTh1hB76RRMquSvnxZrJzQx7h9nQP2PCRL38v6VDb5ip3nf1p"));
            _sender = Contract.CreateSignatureRedeemScript(_keyPair1.PublicKey).ToScriptHash();
            _multiSender = Contract.CreateMultiSigContract(1, new ECPoint[] { _keyPair1.PublicKey }).ScriptHash;
            _rpcClientMock = UT_TransactionManager.MockRpcClient(_sender, Array.Empty<byte>());
            _client = _rpcClientMock.Object;
            _address1 = _sender.ToAddress(_client.ProtocolSettings.AddressVersion);
            _walletApi = new WalletAPI(_rpcClientMock.Object);
        }

        [TestMethod]
        public async Task TestGetUnclaimedGas()
        {
            byte[] testScript = NativeContract.NEO.Hash.MakeScript("unclaimedGas", _sender, 99);
            UT_TransactionManager.MockInvokeScript(_rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(1_10000000) });

            var balance = await _walletApi.GetUnclaimedGasAsync(_address1);
            Assert.AreEqual(1.1m, balance);
        }

        [TestMethod]
        public async Task TestGetNeoBalance()
        {
            byte[] testScript = NativeContract.NEO.Hash.MakeScript("balanceOf", _sender);
            UT_TransactionManager.MockInvokeScript(_rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(1_00000000) });

            var balance = await _walletApi.GetNeoBalanceAsync(_address1);
            Assert.AreEqual(1_00000000u, balance);
        }

        [TestMethod]
        public async Task TestGetGasBalance()
        {
            byte[] testScript = NativeContract.GAS.Hash.MakeScript("balanceOf", _sender);
            UT_TransactionManager.MockInvokeScript(_rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(1_10000000) });

            var balance = await _walletApi.GetGasBalanceAsync(_address1);
            Assert.AreEqual(1.1m, balance);
        }

        [TestMethod]
        public async Task TestGetTokenBalance()
        {
            byte[] testScript = UInt160.Zero.MakeScript("balanceOf", _sender);
            UT_TransactionManager.MockInvokeScript(_rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(1_10000000) });

            var balance = await _walletApi.GetTokenBalanceAsync(UInt160.Zero.ToString(), _address1);
            Assert.AreEqual(1_10000000, balance);
        }

        [TestMethod]
        public async Task TestClaimGas()
        {
            byte[] balanceScript = NativeContract.NEO.Hash.MakeScript("balanceOf", _sender);
            UT_TransactionManager.MockInvokeScript(_rpcClientMock, balanceScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(1_00000000) });

            byte[] testScript = NativeContract.NEO.Hash.MakeScript("transfer", _sender, _sender, new BigInteger(1_00000000), null);
            UT_TransactionManager.MockInvokeScript(_rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(1_10000000) });

            var json = new JObject();
            json["hash"] = UInt256.Zero.ToString();
            _rpcClientMock.Setup(p => p.RpcSendAsync("sendrawtransaction", It.IsAny<JToken>())).ReturnsAsync(json);

            var tranaction = await _walletApi.ClaimGasAsync(_keyPair1.Export(), false);
            Assert.AreEqual(testScript.ToHexString(), tranaction.Script.Span.ToHexString());
        }

        [TestMethod]
        public async Task TestTransfer()
        {
            byte[] decimalsScript = NativeContract.GAS.Hash.MakeScript("decimals");
            UT_TransactionManager.MockInvokeScript(_rpcClientMock, decimalsScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(8) });

            byte[] testScript = NativeContract.GAS.Hash.MakeScript("transfer", _sender, UInt160.Zero, NativeContract.GAS.Factor * 100, null)
                .Concat(new[] { (byte)OpCode.ASSERT })
                .ToArray();
            UT_TransactionManager.MockInvokeScript(_rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(1_10000000) });

            var json = new JObject();
            json["hash"] = UInt256.Zero.ToString();
            _rpcClientMock.Setup(p => p.RpcSendAsync("sendrawtransaction", It.IsAny<JToken>())).ReturnsAsync(json);

            var tranaction = await _walletApi.TransferAsync(NativeContract.GAS.Hash.ToString(), _keyPair1.Export(), UInt160.Zero.ToAddress(_client.ProtocolSettings.AddressVersion), 100, null, true);
            Assert.AreEqual(testScript.ToHexString(), tranaction.Script.Span.ToHexString());
        }

        [TestMethod]
        public async Task TestTransferFromMultiSigAccount()
        {
            byte[] balanceScript = NativeContract.GAS.Hash.MakeScript("balanceOf", _multiSender);
            var balanceResult = new ContractParameter() { Type = ContractParameterType.Integer, Value = BigInteger.Parse("10000000000000000") };

            UT_TransactionManager.MockInvokeScript(_rpcClientMock, balanceScript, balanceResult);

            byte[] decimalsScript = NativeContract.GAS.Hash.MakeScript("decimals");
            UT_TransactionManager.MockInvokeScript(_rpcClientMock, decimalsScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(8) });

            byte[] testScript = NativeContract.GAS.Hash.MakeScript("transfer", _multiSender, UInt160.Zero, NativeContract.GAS.Factor * 100, null)
                .Concat(new[] { (byte)OpCode.ASSERT })
                .ToArray();
            UT_TransactionManager.MockInvokeScript(_rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(1_10000000) });

            var json = new JObject();
            json["hash"] = UInt256.Zero.ToString();
            _rpcClientMock.Setup(p => p.RpcSendAsync("sendrawtransaction", It.IsAny<JToken>())).ReturnsAsync(json);

            var transaction = await _walletApi.TransferAsync(NativeContract.GAS.Hash, 1, new[] { _keyPair1.PublicKey }, new[] { _keyPair1 }, UInt160.Zero, NativeContract.GAS.Factor * 100, null, true);
            Assert.AreEqual(testScript.ToHexString(), transaction.Script.Span.ToHexString());

            try
            {
                transaction = await _walletApi.TransferAsync(NativeContract.GAS.Hash, 2, new[] { _keyPair1.PublicKey }, new[] { _keyPair1 }, UInt160.Zero, NativeContract.GAS.Factor * 100, null, true);
                Assert.Fail();
            }
            catch (System.Exception e)
            {
                Assert.AreEqual(e.Message, $"Need at least 2 KeyPairs for signing!");
            }

            testScript = NativeContract.GAS.Hash.MakeScript("transfer", _multiSender, UInt160.Zero, NativeContract.GAS.Factor * 100, string.Empty)
                .Concat(new[] { (byte)OpCode.ASSERT })
                .ToArray();
            UT_TransactionManager.MockInvokeScript(_rpcClientMock, testScript, new ContractParameter { Type = ContractParameterType.Integer, Value = new BigInteger(1_10000000) });

            transaction = await _walletApi.TransferAsync(NativeContract.GAS.Hash, 1, new[] { _keyPair1.PublicKey }, new[] { _keyPair1 }, UInt160.Zero, NativeContract.GAS.Factor * 100, string.Empty, true);
            Assert.AreEqual(testScript.ToHexString(), transaction.Script.Span.ToHexString());
        }

        [TestMethod]
        public async Task TestWaitTransaction()
        {
            Transaction transaction = TestUtils.GetTransaction();
            _rpcClientMock.Setup(p => p.RpcSendAsync("getrawtransaction", It.Is<JToken[]>(j => j[0].AsString() == transaction.Hash.ToString())))
                .ReturnsAsync(new RpcTransaction { Transaction = transaction, VmState = VMState.HALT, BlockHash = UInt256.Zero, BlockTime = 100, Confirmations = 1 }.ToJson(_client.ProtocolSettings));

            var tx = await _walletApi.WaitTransactionAsync(transaction);
            Assert.AreEqual(VMState.HALT, tx.VmState);
            Assert.AreEqual(UInt256.Zero, tx.BlockHash);
        }
    }
}
