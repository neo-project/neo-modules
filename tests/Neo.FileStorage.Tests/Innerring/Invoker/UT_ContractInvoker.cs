using Akka.Actor;
using Akka.TestKit.Xunit2;
using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.InnerRing.Invoker;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.Tests.InnerRing.Processors;
using Neo.IO;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;
using Container = Neo.FileStorage.API.Container.Container;

namespace Neo.FileStorage.Tests.InnerRing.Invoker
{
    [TestClass]
    public class UT_ContractInvoker : TestKit
    {
        private Client morphclient;
        private Wallet wallet;

        [TestInitialize]
        public void TestSetup()
        {
            NeoSystem system = TestBlockchain.TheNeoSystem;
            system.ActorSystem.ActorOf(Props.Create(() => new ProcessorFakeActor()));
            wallet = TestBlockchain.wallet;
            morphclient = new Client()
            {
                client = new MorphClient()
                {
                    wallet = wallet,
                    system = system
                }
            };
        }

        [TestMethod]
        public void InvokeTransferBalanceXTest()
        {
            bool result = morphclient.TransferBalanceX(UInt160.Zero.ToArray(), UInt160.Zero.ToArray(), 0, new byte[] { 0x01 });
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.AreEqual(result, true);
            Assert.IsNotNull(tx);
        }

        [TestMethod]
        public void InvokeMintTest()
        {
            bool result = morphclient.Mint(Settings.Default.NetmapContractHash.ToArray(), 0, new byte[] { 0x01 });
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.AreEqual(result, true);
            Assert.IsNotNull(tx);
        }

        [TestMethod]
        public void InvokeBurnTest()
        {
            bool result = morphclient.Burn(Settings.Default.NetmapContractHash.ToArray(), 0, new byte[] { 0x01 });
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.AreEqual(result, true);
            Assert.IsNotNull(tx);
        }

        [TestMethod]
        public void InvokeLockAssetTest()
        {
            bool result = morphclient.LockAsset(new byte[] { 0x01 }, UInt160.Zero, UInt160.Zero, 0, 100);
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.AreEqual(result, true);
            Assert.IsNotNull(tx);
        }

        [TestMethod]
        public void InvokeBalancePrecisionTest()
        {
            uint result = morphclient.BalancePrecision();
            Assert.AreEqual(result, (uint)12);
        }

        [TestMethod]
        public void InvokeRegisterContainerTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            OwnerID ownerId = Neo.FileStorage.API.Cryptography.KeyExtension.PublicKeyToOwnerID(key.PublicKey.ToArray());
            Container container = new Container()
            {
                Version = new Neo.FileStorage.API.Refs.Version(),
                BasicAcl = 0,
                Nonce = ByteString.CopyFrom(new byte[16], 0, 16),
                OwnerId = ownerId,
                PlacementPolicy = new PlacementPolicy()
            };
            byte[] sig = Cryptography.Crypto.Sign(container.ToByteArray(), key.PrivateKey, key.PublicKey.EncodePoint(false)[1..]);
            bool result = morphclient.RegisterContainer(key.PublicKey, container.ToByteArray(), sig);
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.AreEqual(result, true);
            Assert.IsNotNull(tx);
        }

        [TestMethod]
        public void InvokeRemoveContainerTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            var containerId = "f7bed8eca63266962baea8067021efb43a94ec7c0f7067926566d60322de9e52".HexToBytes();
            var sig = Cryptography.Crypto.Sign(containerId, key.PrivateKey, key.PublicKey.EncodePoint(false)[1..]);
            var result = morphclient.RemoveContainer(containerId, sig);
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.AreEqual(result, true);
            Assert.IsNotNull(tx);
        }

        [TestMethod]
        public void InvokeGetEpochTest()
        {
            long result = morphclient.GetEpoch();
            Assert.AreEqual(result, 1);
        }

        [TestMethod]
        public void InvokeSetNewEpochTest()
        {
            bool result = morphclient.SetNewEpoch(100);
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.AreEqual(result, true);
            Assert.IsNotNull(tx);
        }

        [TestMethod]
        public void InvokeApproveAndUpdatePeerTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            var nodeInfo = new NodeInfo()
            {
                PublicKey = ByteString.CopyFrom(key.PublicKey.ToArray()),
                Address = API.Cryptography.KeyExtension.PublicKeyToAddress(key.PublicKey.ToArray()),
                State = NodeInfo.Types.State.Online
            };
            bool result = morphclient.ApprovePeer(nodeInfo.ToByteArray());
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.AreEqual(result, true);
            Assert.IsNotNull(tx);
            result = morphclient.UpdatePeerState(key.PublicKey, (int)NodeInfo.Types.State.Offline);
            tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.AreEqual(result, true);
            Assert.IsNotNull(tx);
        }

        [TestMethod]
        public void InvokeSetConfigTest()
        {
            bool result = morphclient.SetConfig(new byte[] { 0x01 }, Neo.Utility.StrictUTF8.GetBytes("ContainerFee"), BitConverter.GetBytes(0));
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.AreEqual(result, true);
            Assert.IsNotNull(tx);
        }

        [TestMethod]
        public void InvokeNetmapSnapshotTest()
        {
            NodeInfo[] result = morphclient.NetmapSnapshot();
            Assert.AreEqual(result.Length, 1);
        }

        [TestMethod]
        public void InvokeAlphabetEmitTest()
        {
            bool result = morphclient.AlphabetEmit(0);
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.AreEqual(result, true);
            Assert.IsNotNull(tx);
        }

        [TestMethod]
        public void InvokeCashOutChequeTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            bool result = morphclient.CashOutCheque(new byte[] { 0x01 }, 1, accounts.ToArray()[0].ScriptHash, accounts.ToArray()[0].ScriptHash);
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.AreEqual(result, true);
            Assert.IsNotNull(tx);
        }
    }
}
