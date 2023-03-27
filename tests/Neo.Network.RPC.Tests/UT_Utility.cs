using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.SmartContract;
using Neo.Wallets;
using System;
using System.Numerics;

namespace Neo.Network.RPC.Tests
{
    [TestClass]
    public class UT_Utility
    {
        private KeyPair _keyPair;
        private UInt160 _scriptHash;
        private ProtocolSettings _protocolSettings;

        [TestInitialize]
        public void TestSetup()
        {
            _keyPair = new KeyPair(Wallet.GetPrivateKeyFromWIF("KyXwTh1hB76RRMquSvnxZrJzQx7h9nQP2PCRL38v6VDb5ip3nf1p"));
            _scriptHash = Contract.CreateSignatureRedeemScript(_keyPair.PublicKey).ToScriptHash();
            _protocolSettings = ProtocolSettings.Load("protocol.json");
        }

        [TestMethod]
        public void TestGetKeyPair()
        {
            string nul = null;
            Assert.ThrowsException<ArgumentNullException>(() => Utility.GetKeyPair(nul));

            string wif = "KyXwTh1hB76RRMquSvnxZrJzQx7h9nQP2PCRL38v6VDb5ip3nf1p";
            var result = Utility.GetKeyPair(wif);
            Assert.AreEqual(_keyPair, result);

            string privateKey = _keyPair.PrivateKey.ToHexString();
            result = Utility.GetKeyPair(privateKey);
            Assert.AreEqual(_keyPair, result);
        }

        [TestMethod]
        public void TestGetScriptHash()
        {
            string nul = null;
            Assert.ThrowsException<ArgumentNullException>(() => Utility.GetScriptHash(nul, _protocolSettings));

            string addr = _scriptHash.ToAddress(_protocolSettings.AddressVersion);
            var result = Utility.GetScriptHash(addr, _protocolSettings);
            Assert.AreEqual(_scriptHash, result);

            string hash = _scriptHash.ToString();
            result = Utility.GetScriptHash(hash, _protocolSettings);
            Assert.AreEqual(_scriptHash, result);

            string publicKey = _keyPair.PublicKey.ToString();
            result = Utility.GetScriptHash(publicKey, _protocolSettings);
            Assert.AreEqual(_scriptHash, result);
        }

        [TestMethod]
        public void TestToBigInteger()
        {
            decimal amount = 1.23456789m;
            uint decimals = 9;
            var result = amount.ToBigInteger(decimals);
            Assert.AreEqual(1234567890, result);

            amount = 1.23456789m;
            decimals = 18;
            result = amount.ToBigInteger(decimals);
            Assert.AreEqual(BigInteger.Parse("1234567890000000000"), result);

            amount = 1.23456789m;
            decimals = 4;
            Assert.ThrowsException<ArgumentException>(() => result = amount.ToBigInteger(decimals));
        }
    }
}
