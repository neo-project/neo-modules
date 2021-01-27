using Akka.Actor;
using Akka.TestKit.Xunit2;
using FSStorageTests.innering.processors;
using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Cryptography;
using Neo.Cryptography.ECC;
using Neo.IO;
using Neo.Plugins.FSStorage.innerring.invoke;
using Neo.Wallets;
using NeoFS.API.v2.Netmap;
using NeoFS.API.v2.Refs;
using System;
using System.Collections.Generic;
using System.Linq;
using Container = NeoFS.API.v2.Container.Container;

namespace Neo.Plugins.FSStorage.morph.invoke.Tests
{
    [TestClass()]
    public class UT_ContractInvoker : TestKit
    {
        private MorphClient morphclient;
        private Wallet wallet;

        [TestInitialize]
        public void TestSetup()
        {
            NeoSystem system = TestBlockchain.TheNeoSystem;
            wallet = TestBlockchain.wallet;
            morphclient = new MorphClient()
            {
                Wallet = wallet,
                Blockchain = system.ActorSystem.ActorOf(Props.Create(() => new ProcessorFakeActor()))
            };
        }

        [TestMethod()]
        public void InvokeTransferBalanceXTest()
        {
            bool result = ContractInvoker.TransferBalanceX(morphclient,
                UInt160.Zero.ToArray(), UInt160.Zero.ToArray(), 0, new byte[] { 0x01 });
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.AreEqual(result, true);
            Assert.IsNotNull(tx);
        }

        [TestMethod()]
        public void InvokeMintTest()
        {
            bool result = ContractInvoker.Mint(morphclient,Settings.Default.NetmapContractHash.ToArray(),0, new byte[] { 0x01 });
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.AreEqual(result, true);
            Assert.IsNotNull(tx);
        }

        [TestMethod()]
        public void InvokeBurnTest()
        {
            bool result = ContractInvoker.Burn(morphclient,Settings.Default.NetmapContractHash.ToArray(), 0, new byte[] { 0x01 });
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.AreEqual(result, true);
            Assert.IsNotNull(tx);
        }

        [TestMethod()]
        public void InvokeLockAssetTest()
        {
            bool result = ContractInvoker.LockAsset(morphclient, new byte[] { 0x01 }, UInt160.Zero, UInt160.Zero, 0, 100);
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.AreEqual(result, true);
            Assert.IsNotNull(tx);
        }

        [TestMethod()]
        public void InvokeBalancePrecisionTest()
        {
            uint result = ContractInvoker.BalancePrecision(morphclient);
            Assert.AreEqual(result, (uint)12);
        }

        [TestMethod()]
        public void InvokeRegisterContainerTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            OwnerID ownerId = NeoFS.API.v2.Cryptography.KeyExtension.PublicKeyToOwnerID(key.PublicKey.ToArray());
            Container container = new Container()
            {
                Version = new NeoFS.API.v2.Refs.Version(),
                BasicAcl = 0,
                Nonce = ByteString.CopyFrom(new byte[16], 0, 16),
                OwnerId = ownerId,
                PlacementPolicy = new PlacementPolicy()
            };
            byte[] sig = Neo.Cryptography.Crypto.Sign(container.ToByteArray(), key.PrivateKey, key.PublicKey.EncodePoint(false)[1..]);
            bool result = ContractInvoker.RegisterContainer(morphclient, key.PublicKey, container.ToByteArray(), sig);
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.AreEqual(result, true);
            Assert.IsNotNull(tx);
        }

        [TestMethod()]
        public void InvokeRemoveContainerTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            var containerId = "f7bed8eca63266962baea8067021efb43a94ec7c0f7067926566d60322de9e52".HexToBytes();
            var sig = Cryptography.Crypto.Sign(containerId, key.PrivateKey, key.PublicKey.EncodePoint(false)[1..]);
            var result = ContractInvoker.RemoveContainer(morphclient, containerId, sig);
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.AreEqual(result, true);
            Assert.IsNotNull(tx);
        }

        [TestMethod()]
        public void InvokeGetEpochTest()
        {
            long result = ContractInvoker.GetEpoch(morphclient);
            Assert.AreEqual(result, 1);
        }

        [TestMethod()]
        public void InvokeSetNewEpochTest()
        {
            bool result = ContractInvoker.SetNewEpoch(morphclient, 100);
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.AreEqual(result, true);
            Assert.IsNotNull(tx);
        }

        [TestMethod()]
        public void InvokeApproveAndUpdatePeerTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            var nodeInfo = new NodeInfo()
            {
                PublicKey = Google.Protobuf.ByteString.CopyFrom(key.PublicKey.ToArray()),
                Address = NeoFS.API.v2.Cryptography.KeyExtension.PublicKeyToAddress(key.PublicKey.ToArray()),
                State = NodeInfo.Types.State.Online
            };
            bool result = ContractInvoker.ApprovePeer(morphclient, nodeInfo.ToByteArray());
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.AreEqual(result, true);
            Assert.IsNotNull(tx);
            result = ContractInvoker.UpdatePeerState(morphclient, key.PublicKey, (int)NodeInfo.Types.State.Offline);
            tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.AreEqual(result, true);
            Assert.IsNotNull(tx);
        }

        [TestMethod()]
        public void InvokeSetConfigTest()
        {
            bool result = ContractInvoker.SetConfig(morphclient, new byte[] { 0x01 }, Neo.Utility.StrictUTF8.GetBytes("ContainerFee"), BitConverter.GetBytes(0));
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.AreEqual(result, true);
            Assert.IsNotNull(tx);
        }

        [TestMethod()]
        public void InvokeUpdateInnerRingTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            bool result = ContractInvoker.UpdateInnerRing(morphclient, new ECPoint[] { key.PublicKey });
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.AreEqual(result, true);
            Assert.IsNotNull(tx);
        }

        [TestMethod()]
        public void InvokeNetmapSnapshotTest()
        {
            NodeInfo[] result = ContractInvoker.NetmapSnapshot(morphclient);
            Assert.AreEqual(result.Length, 1);
        }

        [TestMethod()]
        public void InvokeAlphabetEmitTest()
        {
            bool result = ContractInvoker.AlphabetEmit(morphclient, 0);
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.AreEqual(result, true);
            Assert.IsNotNull(tx);
        }

        [TestMethod()]
        public void InvokeIsInnerRingTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            bool result = ContractInvoker.IsInnerRing(morphclient, key.PublicKey);
            Assert.AreEqual(result, true);
        }

        [TestMethod()]
        public void InvokeInnerRingIndexTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            int result = ContractInvoker.InnerRingIndex(morphclient, key.PublicKey,out int size);
            Assert.AreEqual(result, 0);
            Assert.AreEqual(size, 7);
        }

        [TestMethod()]
        public void InvokeCashOutChequeTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            bool result = ContractInvoker.CashOutCheque(morphclient,
                new byte[] { 0x01 },1, accounts.ToArray()[0].ScriptHash, accounts.ToArray()[0].ScriptHash);
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.AreEqual(result, true);
            Assert.IsNotNull(tx);
        }
    }
}
