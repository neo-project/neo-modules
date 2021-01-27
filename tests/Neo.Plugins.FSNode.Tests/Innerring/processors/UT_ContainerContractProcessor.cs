using Akka.Actor;
using Akka.TestKit.Xunit2;
using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo.Cryptography;
using Neo.IO;
using Neo.Ledger;
using Neo.Plugins.FSStorage.innerring.processors;
using Neo.SmartContract;
using Neo.Wallets;
using NeoFS.API.v2.Container;
using NeoFS.API.v2.Refs;
using System.Collections.Generic;
using System.Linq;
using Neo.VM;
using static Neo.Plugins.FSStorage.morph.invoke.MorphClient;
using static Neo.Plugins.FSStorage.morph.invoke.Tests.UT_BalanceContractProcessor;
using static Neo.Plugins.FSStorage.MorphEvent;
using FSStorageTests.innering.processors;
using System;

namespace Neo.Plugins.FSStorage.morph.invoke.Tests
{
    [TestClass()]
    public class UT_ContainerContractProcessor : TestKit
    {
        private NeoSystem system;
        private ContainerContractProcessor processor;
        private MorphClient morphclient;
        private Wallet wallet;
        private TestActiveState activeState;

        [TestInitialize]
        public void TestSetup()
        {
            system = TestBlockchain.TheNeoSystem;
            wallet = TestBlockchain.wallet;
            activeState = new TestActiveState();
            activeState.SetActive(true);
            morphclient = new MorphClient()
            {
                Wallet = wallet,
                Blockchain = system.ActorSystem.ActorOf(Props.Create(() => new ProcessorFakeActor()))
            };
            processor = new ContainerContractProcessor()
            {
                Client = morphclient,
                ActiveState = activeState,
                WorkPool = system.ActorSystem.ActorOf(Props.Create(() => new ProcessorFakeActor()))
            };
        }

        [TestMethod()]
        public void HandlePutTest()
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
                PlacementPolicy = new NeoFS.API.v2.Netmap.PlacementPolicy()
            };
            byte[] sig = Neo.Cryptography.Crypto.Sign(container.ToByteArray(), key.PrivateKey, key.PublicKey.EncodePoint(false)[1..]);

            processor.HandlePut(new ContainerPutEvent()
            {
                RawContainer = container.ToByteArray(),
                PublicKey = key.PublicKey,
                Signature = sig
            });
            var nt = ExpectMsg<ProcessorFakeActor.OperationResult2>().nt;
            Assert.IsNotNull(nt);
        }

        [TestMethod()]
        public void HandleDeleteTest()
        {
            processor.HandleDelete(new ContainerDeleteEvent()
            {
                ContainerID = new byte[] { 0x01 },
                Signature = new byte[] { 0x01 }
            });
            var nt = ExpectMsg<ProcessorFakeActor.OperationResult2>().nt;
            Assert.IsNotNull(nt);
        }

        [TestMethod()]
        public void ProcessContainerPutTest()
        {
            activeState.SetActive(true);
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            OwnerID ownerId = NeoFS.API.v2.Cryptography.KeyExtension.PublicKeyToOwnerID(key.PublicKey.ToArray());
            Container container = new Container()
            {
                Version = new NeoFS.API.v2.Refs.Version(),
                BasicAcl = 0,
                Nonce = ByteString.CopyFrom(new byte[16], 0, 16),
                OwnerId = ownerId,
                PlacementPolicy = new NeoFS.API.v2.Netmap.PlacementPolicy()
            };
            byte[] sig = Neo.Cryptography.Crypto.Sign(container.ToByteArray(), key.PrivateKey, key.PublicKey.EncodePoint(false)[1..]);
            processor.ProcessContainerPut(new ContainerPutEvent()
            {
                PublicKey = key.PublicKey,
                Signature = sig,
                RawContainer = container.ToByteArray()
            });
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.IsNotNull(tx);

            activeState.SetActive(false);
            processor.ProcessContainerPut(new ContainerPutEvent()
            {
                PublicKey = key.PublicKey,
                Signature = sig,
                RawContainer = container.ToByteArray()
            });
            ExpectNoMsg();
        }

        [TestMethod()]
        public void ProcessContainerDeleteTest()
        {
            activeState.SetActive(true);
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            var containerId = "f7bed8eca63266962baea8067021efb43a94ec7c0f7067926566d60322de9e52".HexToBytes();
            var sig = Neo.Cryptography.Crypto.Sign(containerId, key.PrivateKey, key.PublicKey.EncodePoint(false)[1..]);
            processor.ProcessContainerDelete(new ContainerDeleteEvent()
            {
                ContainerID = containerId,
                Signature = sig
            });
            var tx = ExpectMsg<ProcessorFakeActor.OperationResult1>().tx;
            Assert.IsNotNull(tx);

            activeState.SetActive(false);
            processor.ProcessContainerDelete(new ContainerDeleteEvent()
            {
                ContainerID = containerId,
                Signature = sig
            });
            ExpectNoMsg();
        }

        [TestMethod()]
        public void ListenerHandlersTest()
        {
            var handlerInfos = processor.ListenerHandlers();
            Assert.AreEqual(handlerInfos.Length, 2);
        }

        [TestMethod()]
        public void ListenerParsersTest()
        {
            var parserInfos = processor.ListenerParsers();
            Assert.AreEqual(parserInfos.Length, 2);
        }

        [TestMethod()]
        public void ListenerTimersHandlersTest()
        {
            var handlerInfos = processor.TimersHandlers();
            Assert.AreEqual(0, handlerInfos.Length);
        }

        [TestMethod()]
        public void CheckFormatTest()
        {
            IEnumerable<WalletAccount> accounts = wallet.GetAccounts();
            KeyPair key = accounts.ToArray()[0].GetKey();
            OwnerID ownerId = NeoFS.API.v2.Cryptography.KeyExtension.PublicKeyToOwnerID(key.PublicKey.ToArray());
            Container container = new Container()
            {
                Version = new NeoFS.API.v2.Refs.Version()
                {
                    Major = 1,
                    Minor = 1,
                },
                BasicAcl = 0,
                Nonce = ByteString.CopyFrom(new byte[16], 0, 16),
                OwnerId = ownerId,
                PlacementPolicy = new NeoFS.API.v2.Netmap.PlacementPolicy()
            };
            Action action = () => processor.CheckFormat(container);
            //wrong nonce
            container.Nonce = ByteString.CopyFrom(new byte[15], 0, 15);
            Assert.ThrowsException<Exception>(action);
            //no placementpolicy
            container.PlacementPolicy = null;
            Assert.ThrowsException<Exception>(action);
        }
    }
}
