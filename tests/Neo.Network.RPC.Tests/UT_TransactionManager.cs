using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Neo.Cryptography;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Json;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Network.RPC.Models;
using Neo.SmartContract;
using Neo.SmartContract.Native;
using Neo.VM;
using Neo.Wallets;
using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Neo.Network.RPC.Tests
{
    [TestClass]
    public class UT_TransactionManager
    {
        TransactionManager _txManager;
        Mock<RpcClient> _rpcClientMock;
        Mock<RpcClient> _multiSigMock;
        KeyPair _keyPair1;
        KeyPair _keyPair2;
        UInt160 _sender;
        UInt160 _multiHash;
        RpcClient _client;

        [TestInitialize]
        public void TestSetup()
        {
            _keyPair1 = new KeyPair(Wallet.GetPrivateKeyFromWIF("KyXwTh1hB76RRMquSvnxZrJzQx7h9nQP2PCRL38v6VDb5ip3nf1p"));
            _keyPair2 = new KeyPair(Wallet.GetPrivateKeyFromWIF("L2LGkrwiNmUAnWYb1XGd5mv7v2eDf6P4F3gHyXSrNJJR4ArmBp7Q"));
            _sender = Contract.CreateSignatureRedeemScript(_keyPair1.PublicKey).ToScriptHash();
            _multiHash = Contract.CreateMultiSigContract(2, new ECPoint[] { _keyPair1.PublicKey, _keyPair2.PublicKey }).ScriptHash;
            _rpcClientMock = MockRpcClient(_sender, new byte[1]);
            _client = _rpcClientMock.Object;
            _multiSigMock = MockMultiSig(_multiHash, new byte[1]);
        }

        public static Mock<RpcClient> MockRpcClient(UInt160 sender, byte[] script)
        {
            var mockRpc = new Mock<RpcClient>(MockBehavior.Strict, new Uri("http://seed1.neo.org:10331"), null, null, null);

            // MockHeight
            mockRpc.Setup(p => p.RpcSendAsync("getblockcount")).ReturnsAsync(100).Verifiable();

            // calculatenetworkfee
            var networkfee = new JObject();
            networkfee["networkfee"] = 100000000;
            mockRpc.Setup(p => p.RpcSendAsync("calculatenetworkfee", It.Is<JToken[]>(u => true)))
                .ReturnsAsync(networkfee)
                .Verifiable();

            // MockGasBalance
            byte[] balanceScript = NativeContract.GAS.Hash.MakeScript("balanceOf", sender);
            var balanceResult = new ContractParameter() { Type = ContractParameterType.Integer, Value = BigInteger.Parse("10000000000000000") };

            MockInvokeScript(mockRpc, balanceScript, balanceResult);

            // MockFeePerByte
            byte[] policyScript = NativeContract.Policy.Hash.MakeScript("getFeePerByte");
            var policyResult = new ContractParameter() { Type = ContractParameterType.Integer, Value = BigInteger.Parse("1000") };

            MockInvokeScript(mockRpc, policyScript, policyResult);

            // MockGasConsumed
            var result = new ContractParameter();
            MockInvokeScript(mockRpc, script, result);

            return mockRpc;
        }

        public static Mock<RpcClient> MockMultiSig(UInt160 multiHash, byte[] script)
        {
            var mockRpc = new Mock<RpcClient>(MockBehavior.Strict, new Uri("http://seed1.neo.org:10331"), null, null, null);

            // MockHeight
            mockRpc.Setup(p => p.RpcSendAsync("getblockcount")).ReturnsAsync(100).Verifiable();

            // calculatenetworkfee
            var networkfee = new JObject();
            networkfee["networkfee"] = 100000000;
            mockRpc.Setup(p => p.RpcSendAsync("calculatenetworkfee", It.Is<JToken[]>(u => true)))
                .ReturnsAsync(networkfee)
                .Verifiable();

            // MockGasBalance
            byte[] balanceScript = NativeContract.GAS.Hash.MakeScript("balanceOf", multiHash);
            var balanceResult = new ContractParameter() { Type = ContractParameterType.Integer, Value = BigInteger.Parse("10000000000000000") };

            MockInvokeScript(mockRpc, balanceScript, balanceResult);

            // MockFeePerByte
            byte[] policyScript = NativeContract.Policy.Hash.MakeScript("getFeePerByte");
            var policyResult = new ContractParameter() { Type = ContractParameterType.Integer, Value = BigInteger.Parse("1000") };

            MockInvokeScript(mockRpc, policyScript, policyResult);

            // MockGasConsumed
            var result = new ContractParameter();
            MockInvokeScript(mockRpc, script, result);

            return mockRpc;
        }

        public static void MockInvokeScript(Mock<RpcClient> mockClient, byte[] script, params ContractParameter[] parameters)
        {
            var result = new RpcInvokeResult()
            {
                Stack = parameters.Select(p => p.ToStackItem()).ToArray(),
                GasConsumed = 100,
                Script = Convert.ToBase64String(script),
                State = VMState.HALT
            };

            mockClient.Setup(p => p.RpcSendAsync("invokescript", It.Is<JToken[]>(j =>
                Convert.FromBase64String(j[0].AsString()).SequenceEqual(script))))
                .ReturnsAsync(result.ToJson())
                .Verifiable();
        }

        [TestMethod]
        public async Task TestMakeTransaction()
        {
            Signer[] signers = new Signer[1]
            {
                new Signer
                {
                    Account = _sender,
                    Scopes= WitnessScope.Global
                }
            };

            byte[] script = new byte[1];
            _txManager = await TransactionManager.MakeTransactionAsync(_rpcClientMock.Object, script, signers);

            var tx = _txManager.Tx;
            Assert.AreEqual(WitnessScope.Global, tx.Signers[0].Scopes);
        }

        [TestMethod]
        public async Task TestSign()
        {
            Signer[] signers = new Signer[1]
            {
                new Signer
                {
                    Account  =  _sender,
                    Scopes = WitnessScope.Global
                }
            };

            byte[] script = new byte[1];
            _txManager = await TransactionManager.MakeTransactionAsync(_client, script, signers);
            await _txManager
                .AddSignature(_keyPair1)
                .SignAsync();

            // get signature from Witnesses
            var tx = _txManager.Tx;
            ReadOnlyMemory<byte> signature = tx.Witnesses[0].InvocationScript[2..];

            Assert.IsTrue(Crypto.VerifySignature(tx.GetSignData(_client.protocolSettings.Network), signature.Span, _keyPair1.PublicKey));
            // verify network fee and system fee
            Assert.AreEqual(100000000/*Mock*/, tx.NetworkFee);
            Assert.AreEqual(100, tx.SystemFee);

            // duplicate sign should not add new witness
            await ThrowsAsync<Exception>(async () => await _txManager.AddSignature(_keyPair1).SignAsync());
            Assert.AreEqual(null, _txManager.Tx.Witnesses);

            // throw exception when the KeyPair is wrong
            await ThrowsAsync<Exception>(async () => await _txManager.AddSignature(_keyPair2).SignAsync());
        }

        // https://docs.microsoft.com/en-us/archive/msdn-magazine/2014/november/async-programming-unit-testing-asynchronous-code#testing-exceptions
        static async Task<TException> ThrowsAsync<TException>(Func<Task> action, bool allowDerivedTypes = true)
            where TException : Exception
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                if (allowDerivedTypes && !(ex is TException))
                    throw new Exception("Delegate threw exception of type " +
                    ex.GetType().Name + ", but " + typeof(TException).Name +
                    " or a derived type was expected.", ex);
                if (!allowDerivedTypes && ex.GetType() != typeof(TException))
                    throw new Exception("Delegate threw exception of type " +
                    ex.GetType().Name + ", but " + typeof(TException).Name +
                    " was expected.", ex);
                return (TException)ex;
            }
            throw new Exception("Delegate did not throw expected exception " +
            typeof(TException).Name + ".");
        }

        [TestMethod]
        public async Task TestSignMulti()
        {
            // Cosigner needs multi signature
            Signer[] signers = new Signer[1]
            {
                new Signer
                {
                    Account = _multiHash,
                    Scopes = WitnessScope.Global
                }
            };

            byte[] script = new byte[1];
            _txManager = await TransactionManager.MakeTransactionAsync(_multiSigMock.Object, script, signers);
            await _txManager
                .AddMultiSig(_keyPair1, 2, _keyPair1.PublicKey, _keyPair2.PublicKey)
                .AddMultiSig(_keyPair2, 2, _keyPair1.PublicKey, _keyPair2.PublicKey)
                .SignAsync();
        }

        [TestMethod]
        public async Task TestAddWitness()
        {
            // Cosigner as contract scripthash
            Signer[] signers = new Signer[2]
            {
                new Signer
                {
                    Account = _sender,
                    Scopes = WitnessScope.Global
                },
                new Signer
                {
                    Account = UInt160.Zero,
                    Scopes = WitnessScope.Global
                }
            };

            byte[] script = new byte[1];
            _txManager = await TransactionManager.MakeTransactionAsync(_rpcClientMock.Object, script, signers);
            _txManager.AddWitness(UInt160.Zero);
            _txManager.AddSignature(_keyPair1);
            await _txManager.SignAsync();

            var tx = _txManager.Tx;
            Assert.AreEqual(2, tx.Witnesses.Length);
            Assert.AreEqual(40, tx.Witnesses[0].VerificationScript.Length);
            Assert.AreEqual(66, tx.Witnesses[0].InvocationScript.Length);
        }
    }
}
