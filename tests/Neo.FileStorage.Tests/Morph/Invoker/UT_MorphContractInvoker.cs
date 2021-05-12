using Akka.Actor;
using Akka.TestKit.Xunit2;
using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Container;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.Morph.Invoker;
using Neo.FileStorage.Tests.InnerRing.Processors;
using Neo.IO;
using Neo.Wallets;
using System;
using System.Collections.Generic;
using System.Linq;
using static Neo.FileStorage.Morph.Invoker.MorphContractInvoker;

namespace Neo.FileStorage.Tests.Morph.Invoker
{
    [TestClass]
    public class UT_MorphContractInvoker : TestKit
    {
        private FileStorage.Morph.Invoker.Client client;
        private Wallet wallet;

        [TestInitialize]
        public void TestSetup()
        {
            NeoSystem system = TestBlockchain.TheNeoSystem;
            wallet = TestBlockchain.wallet;
            system.ActorSystem.ActorOf(Props.Create(() => new ProcessorFakeActor()));
            client = new FileStorage.Morph.Invoker.Client()
            {
                client = new MorphClient()
                {
                    wallet = wallet,
                    system = system,
                }
            };
        }

        [TestMethod]
        public void InvokeBalanceOfTest()
        {
            long result = MorphContractInvoker.InvokeBalanceOf(client, UInt160.Zero.ToArray());
            Assert.AreEqual(result, 0);
        }

        [TestMethod]
        public void InvokeDecimalsTest()
        {
            long result = MorphContractInvoker.InvokeDecimals(client);
            Assert.AreEqual(result, 12);
        }

        [TestMethod]
        public void InvokeAddPeerTest()
        {
            var key = wallet.GetAccounts().ToArray()[0].GetKey().PublicKey;
            NodeInfo nodeInfo = new NodeInfo();
            nodeInfo.Address = Neo.FileStorage.API.Cryptography.KeyExtension.PublicKeyToAddress(key.ToArray());
            nodeInfo.PublicKey = ByteString.CopyFrom(key.ToArray());
            var rawNodeInfo = nodeInfo.ToByteArray();
            bool result = MorphContractInvoker.InvokeAddPeer(client, rawNodeInfo);
            Assert.AreEqual(result, true);
        }

        [TestMethod]
        public void InvokeConfigTest()
        {
            ulong result = MorphContractInvoker.ContainerFee(client);
            Assert.AreEqual(0ul, result);
        }

        [TestMethod]
        public void InvokeEpochTest()
        {
            ulong result = MorphContractInvoker.InvokeEpoch(client);
            Assert.AreEqual(result, 1);
        }

        [TestMethod]
        public void InvokeNewEpochTest()
        {
            bool result = MorphContractInvoker.InvokeNewEpoch(client, 1);
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.AreEqual(result, true);
            Assert.IsNotNull(tx);
        }

        [TestMethod]
        public void InvokeUpdateStateTest()
        {
            var key = wallet.GetAccounts().ToArray()[0].GetKey().PublicKey;
            bool result = MorphContractInvoker.InvokeUpdateState(client, 2, key.EncodePoint(true));
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.AreEqual(result, true);
            Assert.IsNotNull(tx);
        }

        [TestMethod]
        public void InvokeSnapshotTest()
        {
            var result = MorphContractInvoker.InvokeSnapshot(client, 0);
            Assert.AreEqual(result.Nodes.Count, 1);
        }

        [TestMethod]
        public void InvokeNetMapTest()
        {
            var result = MorphContractInvoker.InvokeNetMap(client);
            Assert.AreEqual(result.Nodes.Count, 1);
        }

        [TestMethod]
        public void InvokePutTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            Neo.FileStorage.API.Refs.OwnerID ownerId = Neo.FileStorage.API.Cryptography.KeyExtension.PublicKeyToOwnerID(key.PublicKey.ToArray());
            Container container = new Container()
            {
                Version = new Neo.FileStorage.API.Refs.Version(),
                BasicAcl = 0,
                Nonce = ByteString.CopyFrom(new byte[16], 0, 16),
                OwnerId = ownerId,
                PlacementPolicy = new PlacementPolicy()
            };
            byte[] sig = Neo.Cryptography.Crypto.Sign(container.ToByteArray(), key.PrivateKey, key.PublicKey.EncodePoint(false)[1..]);
            bool result = MorphContractInvoker.InvokePut(client, container, key.PublicKey.ToArray(), sig);
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.AreEqual(result, true);
            Assert.IsNotNull(tx);
        }

        [TestMethod]
        public void InvokeDeleteTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            API.Refs.OwnerID ownerId = API.Cryptography.KeyExtension.PublicKeyToOwnerID(key.PublicKey.ToArray());
            Container container = new Container()
            {
                Version = new API.Refs.Version(),
                BasicAcl = 0,
                Nonce = ByteString.CopyFrom(new byte[16], 0, 16),
                OwnerId = ownerId,
                PlacementPolicy = new PlacementPolicy()
            };
            byte[] sig = Cryptography.Crypto.Sign(container.ToByteArray(), key.PrivateKey, key.PublicKey.EncodePoint(false)[1..]);
            bool result = MorphContractInvoker.InvokeDelete(client, container.CalCulateAndGetId, sig);
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.AreEqual(result, true);
            Assert.IsNotNull(tx);
        }

        [TestMethod]
        public void InvokeSetEACLTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            API.Refs.OwnerID ownerId = API.Cryptography.KeyExtension.PublicKeyToOwnerID(key.PublicKey.ToArray());
            Container container = new Container()
            {
                Version = new API.Refs.Version(),
                BasicAcl = 0,
                Nonce = ByteString.CopyFrom(new byte[16], 0, 16),
                OwnerId = ownerId,
                PlacementPolicy = new PlacementPolicy()
            };
            API.Acl.EACLTable eACLTable = new API.Acl.EACLTable()
            {
                ContainerId = container.CalCulateAndGetId,
                Version = new API.Refs.Version(),
            };
            eACLTable.Records.Add(new API.Acl.EACLRecord());
            byte[] sig = Cryptography.Crypto.Sign(eACLTable.ToByteArray(), key.PrivateKey, key.PublicKey.EncodePoint(false)[1..]);
            bool result = MorphContractInvoker.InvokeSetEACL(client, eACLTable, sig);
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.AreEqual(result, true);
            Assert.IsNotNull(tx);
        }

        [TestMethod]
        public void InvokeGetEACLTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            Neo.FileStorage.API.Refs.OwnerID ownerId = Neo.FileStorage.API.Cryptography.KeyExtension.PublicKeyToOwnerID(key.PublicKey.ToArray());
            Container container = new Container()
            {
                Version = new Neo.FileStorage.API.Refs.Version(),
                BasicAcl = 0,
                Nonce = ByteString.CopyFrom(new byte[16], 0, 16),
                OwnerId = ownerId,
                PlacementPolicy = new PlacementPolicy()
            };
            API.Acl.EACLTable eACLTable = new API.Acl.EACLTable()
            {
                ContainerId = container.CalCulateAndGetId,
                Version = new API.Refs.Version(),
            };
            eACLTable.Records.Add(new API.Acl.EACLRecord());
            byte[] sig = Cryptography.Crypto.Sign(eACLTable.ToByteArray(), key.PrivateKey, key.PublicKey.EncodePoint(false)[1..]);
            EAclWithSignature result = MorphContractInvoker.InvokeGetEACL(client, container.CalCulateAndGetId);
            Assert.IsNotNull(result);
            Assert.AreEqual(result.Table.ToByteArray().ToHexString(), eACLTable.ToByteArray().ToHexString());
        }

        [TestMethod]
        public void InvokeGetContainerTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            API.Refs.OwnerID ownerId = API.Cryptography.KeyExtension.PublicKeyToOwnerID(key.PublicKey.ToArray());
            Container container = new Container()
            {
                Version = new API.Refs.Version(),
                BasicAcl = 0,
                Nonce = ByteString.CopyFrom(new byte[16], 0, 16),
                OwnerId = ownerId,
                PlacementPolicy = new PlacementPolicy()
            };
            Container result = MorphContractInvoker.InvokeGetContainer(client, container.CalCulateAndGetId);
            Assert.AreEqual(result.ToByteArray().ToHexString(), container.ToByteArray().ToHexString());
        }

        [TestMethod]
        public void InvokeGetContainerListTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            API.Refs.OwnerID ownerId = API.Cryptography.KeyExtension.PublicKeyToOwnerID(key.PublicKey.ToArray());
            Container container = new Container()
            {
                Version = new API.Refs.Version(),
                BasicAcl = 0,
                Nonce = ByteString.CopyFrom(new byte[16], 0, 16),
                OwnerId = ownerId,
                PlacementPolicy = new PlacementPolicy()
            };
            List<ContainerID> result = MorphContractInvoker.InvokeGetContainerList(client, ownerId);
            Assert.AreEqual(result.Count, 1);
            Assert.AreEqual(result.ElementAt(0).ToByteArray().ToHexString(), container.CalCulateAndGetId.Value.ToByteArray().ToHexString());
        }
    }
}
