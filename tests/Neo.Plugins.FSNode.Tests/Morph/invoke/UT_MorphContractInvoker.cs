using Akka.Actor;
using Akka.TestKit.Xunit2;
using FSStorageTests.innering.processors;
using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Cryptography;
using Neo.IO;
using Neo.Wallets;
using NeoFS.API.v2.Container;
using NeoFS.API.v2.Netmap;
using System;
using System.Collections.Generic;
using System.Linq;
using static Neo.Plugins.FSStorage.morph.invoke.MorphContractInvoker;

namespace Neo.Plugins.FSStorage.morph.invoke.Tests
{
    [TestClass()]
    public class UT_MorphContractInvoker : TestKit
    {
        private MorphClient client;
        private Wallet wallet;

        [TestInitialize]
        public void TestSetup()
        {
            NeoSystem system = TestBlockchain.TheNeoSystem;
            wallet = TestBlockchain.wallet;
            client = new MorphClient()
            {
                Wallet = wallet,
                Blockchain = system.ActorSystem.ActorOf(Props.Create(() => new ProcessorFakeActor()))
            };
        }

        [TestMethod()]
        public void InvokeBalanceOfTest()
        {
            long result = MorphContractInvoker.InvokeBalanceOf(client, UInt160.Zero.ToArray());
            Assert.AreEqual(result, 0);
        }

        [TestMethod()]
        public void InvokeDecimalsTest()
        {
            long result = MorphContractInvoker.InvokeDecimals(client);
            Assert.AreEqual(result, 12);
        }

        [TestMethod()]
        public void InvokeAddPeerTest()
        {
            var key = wallet.GetAccounts().ToArray()[0].GetKey().PublicKey;
            NodeInfo nodeInfo = new NodeInfo();
            nodeInfo.Address = NeoFS.API.v2.Cryptography.KeyExtension.PublicKeyToAddress(key.ToArray());
            nodeInfo.PublicKey = ByteString.CopyFrom(key.ToArray());
            var rawNodeInfo = nodeInfo.ToByteArray();
            bool result = MorphContractInvoker.InvokeAddPeer(client, rawNodeInfo);
            Assert.AreEqual(result, true);
        }

        [TestMethod()]
        public void InvokeConfigTest()
        {
            var key = Neo.Utility.StrictUTF8.GetBytes("ContainerFee");
            byte[] result = MorphContractInvoker.InvokeConfig(client, key);
            Assert.AreEqual(result.ToHexString(), BitConverter.GetBytes(0).ToHexString());
        }

        [TestMethod()]
        public void InvokeEpochTest()
        {
            long result = MorphContractInvoker.InvokeEpoch(client);
            Assert.AreEqual(result, 1);
        }

        [TestMethod()]
        public void InvokeNewEpochTest()
        {
            bool result = MorphContractInvoker.InvokeNewEpoch(client, 1);
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.AreEqual(result, true);
            Assert.IsNotNull(tx);
        }

        [TestMethod()]
        public void InvokeInnerRingListTest()
        {
            byte[][] result = MorphContractInvoker.InvokeInnerRingList(client);
            Assert.AreEqual(result.Length, 7);
        }

        [TestMethod()]
        public void InvokeUpdateStateTest()
        {
            var key = wallet.GetAccounts().ToArray()[0].GetKey().PublicKey;
            bool result = MorphContractInvoker.InvokeUpdateState(client, new UpdateStateArgs()
            {
                key = key.EncodePoint(true),
                state = 2
            });
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.AreEqual(result, true);
            Assert.IsNotNull(tx);
        }

        [TestMethod()]
        public void InvokeSnapshotTest()
        {
            byte[][] result = MorphContractInvoker.InvokeSnapshot(client, 0);
            Assert.AreEqual(result.Length, 1);
        }

        [TestMethod()]
        public void InvokeNetMapTest()
        {
            byte[][] result = MorphContractInvoker.InvokeNetMap(client);
            Assert.AreEqual(result.Length, 1);
        }

        [TestMethod()]
        public void InvokePutTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            NeoFS.API.v2.Refs.OwnerID ownerId = NeoFS.API.v2.Cryptography.KeyExtension.PublicKeyToOwnerID(key.PublicKey.ToArray());
            Container container = new Container()
            {
                Version = new NeoFS.API.v2.Refs.Version(),
                BasicAcl = 0,
                Nonce = ByteString.CopyFrom(new byte[16], 0, 16),
                OwnerId = ownerId,
                PlacementPolicy = new PlacementPolicy()
            };
            byte[] sig = Neo.Cryptography.Crypto.Sign(container.ToByteArray(), key.PrivateKey, key.PublicKey.EncodePoint(false)[1..]);
            bool result = MorphContractInvoker.InvokePut(client, new PutArgs()
            {
                cnr = container.ToByteArray(),
                publicKey = key.PublicKey.ToArray(),
                sig = sig
            });
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.AreEqual(result, true);
            Assert.IsNotNull(tx);
        }

        [TestMethod()]
        public void InvokeDeleteTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            NeoFS.API.v2.Refs.OwnerID ownerId = NeoFS.API.v2.Cryptography.KeyExtension.PublicKeyToOwnerID(key.PublicKey.ToArray());
            Container container = new Container()
            {
                Version = new NeoFS.API.v2.Refs.Version(),
                BasicAcl = 0,
                Nonce = ByteString.CopyFrom(new byte[16], 0, 16),
                OwnerId = ownerId,
                PlacementPolicy = new PlacementPolicy()
            };
            byte[] sig = Neo.Cryptography.Crypto.Sign(container.ToByteArray(), key.PrivateKey, key.PublicKey.EncodePoint(false)[1..]);
            bool result = MorphContractInvoker.InvokeDelete(client, new DeleteArgs()
            {
                cid = container.CalCulateAndGetID.Value.ToByteArray(),
                sig = sig
            });
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.AreEqual(result, true);
            Assert.IsNotNull(tx);
        }

        [TestMethod()]
        public void InvokeSetEACLTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            NeoFS.API.v2.Refs.OwnerID ownerId = NeoFS.API.v2.Cryptography.KeyExtension.PublicKeyToOwnerID(key.PublicKey.ToArray());
            Container container = new Container()
            {
                Version = new NeoFS.API.v2.Refs.Version(),
                BasicAcl = 0,
                Nonce = ByteString.CopyFrom(new byte[16], 0, 16),
                OwnerId = ownerId,
                PlacementPolicy = new PlacementPolicy()
            };
            NeoFS.API.v2.Acl.EACLTable eACLTable = new NeoFS.API.v2.Acl.EACLTable()
            {
                ContainerId = container.CalCulateAndGetID,
                Version = new NeoFS.API.v2.Refs.Version(),
            };
            eACLTable.Records.Add(new NeoFS.API.v2.Acl.EACLRecord());
            byte[] sig = Neo.Cryptography.Crypto.Sign(eACLTable.ToByteArray(), key.PrivateKey, key.PublicKey.EncodePoint(false)[1..]);
            bool result = MorphContractInvoker.InvokeSetEACL(client, new SetEACLArgs()
            {
                eacl = eACLTable.ToByteArray(),
                sig = sig
            });
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.AreEqual(result, true);
            Assert.IsNotNull(tx);
        }

        [TestMethod()]
        public void InvokeGetEACLTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            NeoFS.API.v2.Refs.OwnerID ownerId = NeoFS.API.v2.Cryptography.KeyExtension.PublicKeyToOwnerID(key.PublicKey.ToArray());
            Container container = new Container()
            {
                Version = new NeoFS.API.v2.Refs.Version(),
                BasicAcl = 0,
                Nonce = ByteString.CopyFrom(new byte[16], 0, 16),
                OwnerId = ownerId,
                PlacementPolicy = new PlacementPolicy()
            };
            NeoFS.API.v2.Acl.EACLTable eACLTable = new NeoFS.API.v2.Acl.EACLTable()
            {
                ContainerId = container.CalCulateAndGetID,
                Version = new NeoFS.API.v2.Refs.Version(),
            };
            eACLTable.Records.Add(new NeoFS.API.v2.Acl.EACLRecord());
            byte[] sig = Neo.Cryptography.Crypto.Sign(eACLTable.ToByteArray(), key.PrivateKey, key.PublicKey.EncodePoint(false)[1..]);
            EACLValues result = MorphContractInvoker.InvokeGetEACL(client, container.CalCulateAndGetID.Value.ToByteArray());
            Assert.IsNotNull(result);
            Assert.AreEqual(result.eacl.ToHexString(), eACLTable.ToByteArray().ToHexString());
        }

        [TestMethod()]
        public void InvokeGetContainerTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            NeoFS.API.v2.Refs.OwnerID ownerId = NeoFS.API.v2.Cryptography.KeyExtension.PublicKeyToOwnerID(key.PublicKey.ToArray());
            Container container = new Container()
            {
                Version = new NeoFS.API.v2.Refs.Version(),
                BasicAcl = 0,
                Nonce = ByteString.CopyFrom(new byte[16], 0, 16),
                OwnerId = ownerId,
                PlacementPolicy = new PlacementPolicy()
            };
            byte[] result = MorphContractInvoker.InvokeGetContainer(client, container.CalCulateAndGetID.Value.ToByteArray());
            Assert.AreEqual(result.ToHexString(), container.ToByteArray().ToHexString());
        }

        [TestMethod()]
        public void InvokeGetContainerListTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            NeoFS.API.v2.Refs.OwnerID ownerId = NeoFS.API.v2.Cryptography.KeyExtension.PublicKeyToOwnerID(key.PublicKey.ToArray());
            Container container = new Container()
            {
                Version = new NeoFS.API.v2.Refs.Version(),
                BasicAcl = 0,
                Nonce = ByteString.CopyFrom(new byte[16], 0, 16),
                OwnerId = ownerId,
                PlacementPolicy = new PlacementPolicy()
            };
            byte[][] result = MorphContractInvoker.InvokeGetContainerList(client, ownerId.Value.ToByteArray());
            Assert.AreEqual(result.Length, 1);
            Assert.AreEqual(result[0].ToHexString(), container.CalCulateAndGetID.Value.ToByteArray().ToHexString());
        }
    }
}
