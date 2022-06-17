using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Akka.Actor;
using Akka.TestKit.Xunit2;
using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.FileStorage.API.Client;
using Neo.FileStorage.API.Container;
using Neo.FileStorage.API.Cryptography;
using Neo.FileStorage.API.Netmap;
using Neo.FileStorage.API.Refs;
using Neo.FileStorage.API.Session;
using Neo.FileStorage.Invoker.Morph;
using Neo.IO;
using Neo.Network.P2P.Payloads;
using Neo.Wallets;

namespace Neo.FileStorage.Tests.Morph.Invoker
{
    [TestClass]
    public class UT_MorphContractInvoker : TestKit
    {
        private MorphInvoker invoker;
        private Wallet wallet;

        [TestInitialize]
        public void TestSetup()
        {
            NeoSystem system = TestBlockchain.TheNeoSystem;
            wallet = TestBlockchain.wallet;
            system.ActorSystem.ActorOf(Props.Create(() => new ProcessorFakeActor()));
            invoker = new MorphInvoker()
            {
                Wallet = wallet,
                NeoSystem = system,
                Blockchain = this.TestActor,
                AlphabetContractHash = TestBlockchain.AlphabetContractHash,
                AuditContractHash = TestBlockchain.AuditContractHash,
                BalanceContractHash = TestBlockchain.BalanceContractHash,
                ContainerContractHash = TestBlockchain.ContainerContractHash,
                FsIdContractHash = TestBlockchain.FsIdContractHash,
                NetMapContractHash = TestBlockchain.NetmapContractHash,
                ReputationContractHash = TestBlockchain.ReputationContractHash,
            };
        }

        [TestMethod]
        public void InvokeBalanceOfTest()
        {
            long result = invoker.BalanceOf(UInt160.Zero.ToArray());
            Assert.AreEqual(result, 0);
        }

        [TestMethod]
        public void InvokeDecimalsTest()
        {
            long result = invoker.BalanceDecimals();
            Assert.AreEqual(result, 12);
        }

        [TestMethod]
        public void InvokeAddPeerTest()
        {
            var key = wallet.GetAccounts().ToArray()[0].GetKey().PublicKey;
            NodeInfo nodeInfo = new()
            {
                PublicKey = ByteString.CopyFrom(key.ToArray())
            };
            invoker.AddPeer(nodeInfo);
        }

        [TestMethod]
        public void InvokeConfigTest()
        {
            ulong result = invoker.ContainerFee();
            Assert.AreEqual(0ul, result);
        }

        [TestMethod]
        public void InvokeEpochTest()
        {
            ulong result = invoker.Epoch();
            Assert.AreEqual(result, 1uL);
        }

        [TestMethod]
        public void InvokeNewEpochTest()
        {
            invoker.NewEpoch(2);
            var tx = ExpectMsg<Transaction>();
            Assert.IsNotNull(tx);
        }

        [TestMethod]
        public void InvokeUpdateStateTest()
        {
            var key = wallet.GetAccounts().ToArray()[0].GetKey().PublicKey;
            invoker.UpdatePeerState(NodeInfo.Types.State.Online, key.EncodePoint(true));
            var tx = ExpectMsg<Transaction>();
            Assert.IsNotNull(tx);
        }

        [TestMethod]
        public void InvokeSnapshotTest()
        {
            var result = invoker.GetNetMapByDiff(0);
            Assert.AreEqual(result.Nodes.Count, 1);
        }

        [TestMethod]
        public void InvokeNetMapTest()
        {
            var result = invoker.NetMap();
            Assert.AreEqual(result.Length, 1);
        }

        [TestMethod]
        public void InvokePutTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair kp = accounts.ToArray()[0].GetKey();
            ECDsa key = kp.PrivateKey.LoadPrivateKey();
            OwnerID owner = OwnerID.FromPublicKey(kp.PublicKey.EncodePoint(true));
            Container container = new()
            {
                Version = new API.Refs.Version(),
                BasicAcl = 0,
                Nonce = ByteString.CopyFrom(new byte[16], 0, 16),
                OwnerId = owner,
                PlacementPolicy = new PlacementPolicy()
            };
            SignatureRFC6979 sig = key.SignRFC6979(container);
            SessionToken token = new()
            {
                Body = new()
                {
                    OwnerId = owner,
                    Id = ByteString.CopyFrom(Guid.NewGuid().ToByteArray()),
                    SessionKey = ByteString.CopyFrom(Guid.NewGuid().ToByteArray()),
                    Lifetime = new()
                }
            };
            token.Signature = key.SignMessagePart(token.Body);
            invoker.PutContainer(container, sig, token);
            var tx = ExpectMsg<Transaction>();
            Assert.IsNotNull(tx);
        }

        [TestMethod]
        public void InvokeDeleteTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair kp = accounts.ToArray()[0].GetKey();
            ECDsa key = kp.PrivateKey.LoadPrivateKey();
            OwnerID ownerId = OwnerID.FromPublicKey(kp.PublicKey.EncodePoint(true));
            Container container = new()
            {
                Version = new API.Refs.Version(),
                BasicAcl = 0,
                Nonce = ByteString.CopyFrom(new byte[16], 0, 16),
                OwnerId = ownerId,
                PlacementPolicy = new PlacementPolicy()
            };
            byte[] sig = key.SignRFC6979(container.ToByteArray());
            SessionToken token = new()
            {
                Body = new()
                {
                    OwnerId = ownerId,
                    Id = ByteString.CopyFrom(Guid.NewGuid().ToByteArray()),
                    SessionKey = ByteString.CopyFrom(Guid.NewGuid().ToByteArray()),
                    Lifetime = new()
                }
            };
            token.Signature = key.SignMessagePart(token.Body);
            invoker.DeleteContainer(container.CalCulateAndGetId, sig, token);
            var tx = ExpectMsg<Transaction>();
            Assert.IsNotNull(tx);
        }

        [TestMethod]
        public void InvokeSetEACLTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair kp = accounts.ToArray()[0].GetKey();
            ECDsa key = kp.PrivateKey.LoadPrivateKey();
            OwnerID ownerId = OwnerID.FromPublicKey(kp.PublicKey.EncodePoint(true));
            Container container = new()
            {
                Version = new API.Refs.Version(),
                BasicAcl = 0,
                Nonce = ByteString.CopyFrom(new byte[16], 0, 16),
                OwnerId = ownerId,
                PlacementPolicy = new PlacementPolicy()
            };
            API.Acl.EACLTable eACLTable = new()
            {
                ContainerId = container.CalCulateAndGetId,
                Version = new API.Refs.Version(),
            };
            eACLTable.Records.Add(new API.Acl.EACLRecord());
            SignatureRFC6979 sig = key.SignRFC6979(eACLTable);
            SessionToken token = new()
            {
                Body = new()
                {
                    OwnerId = ownerId,
                    Id = ByteString.CopyFrom(Guid.NewGuid().ToByteArray()),
                    SessionKey = ByteString.CopyFrom(Guid.NewGuid().ToByteArray()),
                    Lifetime = new()
                }
            };
            token.Signature = key.SignMessagePart(token.Body);
            invoker.SetEACL(eACLTable, sig, token);
            var tx = ExpectMsg<Transaction>();
            Assert.IsNotNull(tx);
        }

        [TestMethod]
        public void InvokeGetEACLTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            OwnerID ownerId = OwnerID.FromPublicKey(key.PublicKey.EncodePoint(true));
            Container container = new()
            {
                Version = new API.Refs.Version(),
                BasicAcl = 0,
                Nonce = ByteString.CopyFrom(new byte[16], 0, 16),
                OwnerId = ownerId,
                PlacementPolicy = new PlacementPolicy()
            };
            API.Acl.EACLTable eACLTable = new()
            {
                ContainerId = container.CalCulateAndGetId,
                Version = new API.Refs.Version(),
            };
            eACLTable.Records.Add(new API.Acl.EACLRecord());
            _ = key.PrivateKey.LoadPrivateKey().SignRFC6979(eACLTable.ToByteArray());
            EAclWithSignature result = invoker.GetEACL(container.CalCulateAndGetId);
            Assert.IsNotNull(result);
            Assert.AreEqual(result.Table.ToByteArray().ToHexString(), eACLTable.ToByteArray().ToHexString());
        }

        [TestMethod]
        public void InvokeGetContainerTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            OwnerID ownerId = OwnerID.FromPublicKey(key.PublicKey.EncodePoint(true));
            Container container = new()
            {
                Version = new API.Refs.Version(),
                BasicAcl = 0,
                Nonce = ByteString.CopyFrom(new byte[16], 0, 16),
                OwnerId = ownerId,
                PlacementPolicy = new PlacementPolicy()
            };
            ContainerWithSignature result = invoker.GetContainer(container.CalCulateAndGetId);
            Assert.AreEqual(result.Container.ToByteArray().ToHexString(), container.ToByteArray().ToHexString());
        }

        [TestMethod]
        public void InvokeGetContainerListTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            OwnerID ownerId = OwnerID.FromPublicKey(key.PublicKey.EncodePoint(true));
            Container container = new()
            {
                Version = new API.Refs.Version(),
                BasicAcl = 0,
                Nonce = ByteString.CopyFrom(new byte[16], 0, 16),
                OwnerId = ownerId,
                PlacementPolicy = new PlacementPolicy()
            };
            List<ContainerID> result = invoker.ListContainers(ownerId);
            Assert.AreEqual(result.Count, 1);
            Assert.AreEqual(result.ElementAt(0).ToByteArray().ToHexString(), container.CalCulateAndGetId.ToByteArray().ToHexString());
        }
    }
}
